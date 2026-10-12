# Technisches Konzept: NetWatch

Stand: 12.10.2026, Stack A (C# mit .NET 10) gewählt.

## Laufumgebung

- Private Windows-10/11-PCs (64 Bit) von Nils und den Teamkollegen, per LAN am Router.
- NetWatch selbst braucht keine Administratorrechte und keine Installation.
- Netzwerk: nur ausgehende ICMP-Pings an Router und öffentliche Ziele; kein weiterer Datenverkehr.
- Entwicklung: Claude Code läuft lokal auf Nils' Windows-PC und kann dort Ping-, Tray- und Standby-Tests selbst ausführen.
- Annahme: Nils hat auf seinem PC Installationsrechte für die Entwicklungsumgebung (.NET 10 SDK, Claude Code).
- Vorkenntnisse: weder C# noch Python; C# ist Lernsprache. Claude Code erklärt deshalb neue Sprachkonzepte beim Umsetzen.

## Stack-Vorschläge

| | A) C# mit .NET 10 und WinForms (Empfehlung) | B) Python 3.12 mit pystray |
| --- | --- | --- |
| Tray-Symbol | `NotifyIcon`, in .NET enthalten | `pystray` (LGPL-3.0) und `Pillow` |
| Ping ohne Adminrechte | `System.Net.NetworkInformation.Ping`, enthalten | Windows-API `IcmpSendEcho` über `ctypes`, selbst gekapselt |
| Standby erkennen | `SystemEvents.PowerModeChanged`, enthalten | eigenes verstecktes Fenster über `ctypes` oder `pywin32` |
| Autostart | Registry `HKCU\...\Run`, enthalten | `winreg`, enthalten |
| Auslieferung | eine .exe (self-contained, ca. 70–150 MB, Annahme) | eine .exe über PyInstaller (ca. 15–30 MB, Annahme) |
| Fremdabhängigkeiten | keine zur Laufzeit | drei bis vier Pakete |
| Risiko | neue Sprache, falls du C# nicht kennst | PyInstaller-Dateien lösen öfter Virenscanner-Fehlalarme aus, was die Verteilung ans Team erschwert |
| Lernwert | Windows-Desktop-Entwicklung, typisiertes Programmieren | vertrauter für viele Ingenieure, Windows-Interna über `ctypes` |

**Empfehlung A:** Alles, was NetWatch braucht, ist in .NET bereits enthalten und für Windows gemacht. Es gibt keine Laufzeitabhängigkeiten mit Lizenzfragen, und die .exe ist für Teamkollegen unauffälliger. .NET 10 ist eine Version mit Langzeitunterstützung bis November 2028; .NET 8 endet im November 2026 und wäre deshalb die falsche Wahl.

## Architektur

```
                 +-----------------+
 Settings.json ->|  SettingsStore  |
                 +--------+--------+
                          v
+-------------+   ProbeResult   +----------------+  Outage  +-----------------+
|   Prober    |---------------->| OutageDetector |--------->|  EventLogWriter |-> netwatch_events.csv
| (ICMP, je   |                 | (Zustands-     |          | (CSV, Puffer bei|
|  Ziel, Takt)|                 |  automat)      |          |  gesperrter Datei)
+------+------+                 +-------+--------+          +-----------------+
       ^                                | Status
GatewayResolver                         v
(Standardroute)                  +-------------+     +-----------------+
                                 |   TrayUi    |     | SessionTracker  |-> netwatch_sessions.csv
                                 | (Symbol,    |     | (Start, Ende,   |
                                 |  Menü)      |     |  Standby, Crash)|
                                 +-------------+     +-----------------+
                                        |
                                        v  (Soll, nach dem MVP)
                                 +----------------+
                                 | ReportGenerator|-> report_day.html, report_week.html
                                 +----------------+
```

- **Prober:** sendet pro Ziel im Prüftakt einen Ping mit Zeitlimit kleiner als der Takt; die Ziele laufen parallel.
- **GatewayResolver:** ermittelt das Standard-Gateway und prüft es alle 60 s erneut.
- **OutageDetector:** reine Logik ohne Windows-Abhängigkeit. Erhält Prüfergebnisse und eine Uhr von außen, damit Tests mit simulierten Daten laufen.
- **CauseClassifier** (Teil des Detektors): `pc_lan`, wenn der Adapter getrennt ist; `router`, wenn auch das Gateway nicht antwortet; sonst `provider`.
- **EventLogWriter und SessionTracker:** schreiben CSV-Dateien in `%LOCALAPPDATA%\NetWatch\` und erkennen einen Absturz an einer offenen Sitzung beim nächsten Start.
- **TrayUi:** Symbol grün, rot oder grau, Hover-Text, Menü mit „Protokollordner öffnen“, „Autostart“, „Beenden“.
- **ReportGenerator (Soll):** erzeugt HTML-Berichte mit selbst gezeichnetem SVG, öffnet sie im Standardbrowser; keine Diagrammbibliothek.

## Zeitbehandlung

- Zeitpunkte kommen von der Systemuhr in UTC, Dauern von einer monotonen Uhr (`Stopwatch`). So bleibt die Dauer auch bei Zeitumstellung oder Uhrsynchronisation richtig.
- In die CSV kommen UTC und Ortszeit mit Versatz.

## Datenmodell

| Objekt | Felder |
| --- | --- |
| ProbeResult | `Target` (IP), `SentAtUtc`, `RttMs` (leer bei Verlust) |
| Outage | `StartUtc`, `EndUtc`, `StartLocal`, `EndLocal`, `DurationMs`, `Cause`, `FailedTargets`, `Gateway` |
| Session | `StartUtc`, `EndUtc`, `EndReason` (`exit`, `standby`, `hibernate`, `crash_detected`) |
| Settings | `IntervalMs` (200), `MinLostProbes` (2), `InternetTargets` (`1.1.1.1`, `8.8.8.8`), `Sensitivity` (`normal`) |

Einheiten: Zeiten in Millisekunden, Zeitstempel ISO 8601.

## Abhängigkeiten und Lizenzen (Stack A)

| Paket | Zweck | Lizenz |
| --- | --- | --- |
| .NET 10 SDK und Runtime, WinForms | Laufzeit, Tray, Ping, Energieereignisse | MIT |
| xUnit | automatische Tests, nur Entwicklung | Apache-2.0 |

## Teststrategie

- **Automatisch (läuft auch ohne Windows-Netzwerk):** Detektor, Ursachenzuordnung, CSV-Format und Zeitumstellung mit simuliertem Prüfer und simulierter Uhr. Referenzfälle sind die Akzeptanztests aus `requirements.md` (1,5-s-Ausfall, einzelner Paketverlust, Router/Provider/Adapter, Zeitumstellung 25.10.2026).
- **Manuell auf Windows:** LAN-Kabel 5 s ziehen, Router neu starten, Standby, Autostart, Excel-Ansicht.
- **Vergleichsmessung:** optional parallel mit einer PingPlotter-Testversion.

## Auslieferung

- Eine selbstständige `NetWatch.exe` (win-x64, self-contained, single file), als ZIP an das Team.
- Keine Code-Signatur. Windows SmartScreen warnt beim ersten Start („Trotzdem ausführen“); das ist für das Team in Ordnung (Entscheidung Nils). Die README erklärt diesen Schritt.

## Technische Risiken und Spikes für Phase 4

| Nr. | Risiko | Spike |
| --- | --- | --- |
| S1 | Pings im 200-ms-Takt an drei Ziele laufen ohne Adminrechte nicht stabil oder verzögern sich. | 10 Minuten messen: Zahl der Prüfungen, Zeitabstände, CPU-Last. |
| S2 | Standby und Aufwachen werden nicht zuverlässig erkannt. | Ereignisse beim Standby protokollieren. |
| S3 | Die Fritzbox beantwortet keine Pings vom LAN. | Beim Start Gateway-Ping prüfen. |
| S4 | Virenscanner oder SmartScreen blockieren die .exe bei Teamkollegen. | Testversion auf einem zweiten PC starten. |
