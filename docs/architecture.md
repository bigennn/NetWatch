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

- **Prober** (`IcmpProber`, App): pingt alle Ziele einer Prüfrunde gleichzeitig mit Zeitlimit 180 ms bei 200 ms Takt. Weil Windows das Zeitlimit beim Adapterwechsel nicht immer einhält (M1: bis 504 ms), bekommt er in M3 ein eigenes Zeitlimit, und eine hängende Prüfung hält die nächste Runde nicht auf.
- **GatewayResolver** (App): fragt Windows nach dem Adapter mit der besten Route zu 1.1.1.1 (`GetBestInterface`, iphlpapi.dll) und nimmt dessen IPv4-Gateway; prüft es alle 60 s erneut.
- **OutageDetector** (Core): reine Logik ohne Windows-Abhängigkeit, Zustandsautomat. Erhält Prüfrunden (`ProbeRound`) mit ihren Zeitstempeln, braucht deshalb selbst keine Uhr. Meldet `OutageStarted` (sobald 2 Runden verloren sind, Beginn rückdatiert) und `OutageEnded`, führt den `ConnectionState` (grau/grün/rot) und kennt `Suspend` und `Stop`.
- **Ursachenzuordnung** (Teil des Detektors), je Runde: `pc_lan`, wenn der Adapter getrennt ist oder jeder Ping „General Failure“ (11050) liefert; `unknown`, wenn kein Gateway bekannt ist oder der Router keine Pings beantwortet; `router`, wenn auch das Gateway nicht antwortet; sonst `provider`. Über den Aussetzer gilt die am weitesten vorne liegende Ursache.
- **EventLogWriter und SessionTracker:** schreiben CSV-Dateien in `%LOCALAPPDATA%\NetWatch\` und erkennen einen Absturz an einer offenen Sitzung beim nächsten Start.
- **TrayUi:** Symbol grün, rot oder grau, Hover-Text, Menü mit „Protokollordner öffnen“, „Autostart“, „Beenden“.
- **ReportGenerator (Soll):** erzeugt HTML-Berichte mit selbst gezeichnetem SVG, öffnet sie im Standardbrowser; keine Diagrammbibliothek.

## Zeitbehandlung

- Zeitpunkte kommen von der Systemuhr in UTC, Dauern von einer monotonen Uhr (`Stopwatch`). So bleibt die Dauer auch bei Zeitumstellung oder Uhrsynchronisation richtig.
- In die CSV kommen UTC und Ortszeit mit Versatz.

## Datenmodell

| Objekt | Felder |
| --- | --- |
| ProbeResult | `Round`, `Target` (IP), `SentAtUtc`, `SentAtMonotonicMs`, `RttMs` (leer bei Verlust), `Status` (Windows-Status, z. B. `Success`, `TimedOut`, `11050`) |
| ProbeRound | `Number`, `SentAtUtc`, `SentAtMonotonicMs`, `Gateway` (ProbeResult oder leer), `Internet` (ProbeResults), `AdapterDown` |
| Outage | `StartUtc`, `EndUtc` (= Beginn + Dauer), `DurationMs`, `Cause` (`PcLan`, `Router`, `Provider`, `Unknown`), `FailedTargets`, `Gateway`, `Note` (`suspended`, `app_exit`, `end_unknown`). Die Ortszeit berechnet der CSV-Schreiber mit `Timestamps`. |
| Session | `StartUtc`, `StartLocal`, `StartReason` (`start`, `resume`), `EndUtc`, `EndLocal`, `EndReason` (`exit`, `suspend`, `shutdown`, `crash_detected`) |
| Settings | `settings.json` in `%LOCALAPPDATA%\NetWatch\`: `Sensitivity` (`normal`; bestimmt den Takt: fein 100 ms, normal 200 ms, grob 500 ms), `MinLostRounds` (2), `InternetTargets` (`1.1.1.1`, `8.8.8.8`) |

Einheiten: Zeiten in Millisekunden, Zeitstempel ISO 8601 mit Millisekunden.

## Abhängigkeiten und Lizenzen (Stack A)

| Paket | Zweck | Lizenz |
| --- | --- | --- |
| .NET 10 SDK und Runtime, WinForms | Laufzeit, Tray, Ping, Energieereignisse | MIT |
| xUnit (`xunit.v3` 4.0.2) | automatische Tests, nur Entwicklung | Apache-2.0 |
| Microsoft.Testing.Platform (über `xunit.v3`) | Testausführung mit `dotnet test`, nur Entwicklung | MIT |

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
