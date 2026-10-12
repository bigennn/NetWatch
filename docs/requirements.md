# Anforderungen: NetWatch

Stand: 12.10.2026, Prioritäten von Nils eingeordnet.

## Begriffe

- **Prüfung:** eine einzelne Erreichbarkeitsanfrage (Ping) an ein Prüfziel.
- **Prüfziele:** der Router (Standard-Gateway, automatisch ermittelt) und zwei öffentliche Internetziele (Vorschlag: 1.1.1.1 und 8.8.8.8).
- **Aussetzer:** ein Zeitraum, in dem kein Internetziel antwortet. Er beginnt mit der ersten verlorenen Prüfung einer Serie und endet mit der ersten erfolgreichen Antwort.
- **Prüftakt:** Abstand zwischen zwei Prüfungen desselben Ziels. Standard: 200 ms (von Nils gewählt). Lücken, die kürzer als der Prüftakt sind, können nicht sicher erkannt werden.

## Funktionale Anforderungen

| ID | Nutzergeschichte | Priorität |
| --- | --- | --- |
| F-01 | Als Fahrer will ich, dass NetWatch Router und Internetziele dauerhaft im Prüftakt prüft, damit auch kurze Aussetzer auffallen. | Muss |
| F-02 | Als Fahrer will ich, dass jeder Aussetzer mit Beginn, Ende und Dauer (ms-genau) erkannt wird, damit ich Zeitpunkte sekundengenau kenne. | Muss |
| F-03 | Als Fahrer will ich, dass jeder Aussetzer eine Ursache bekommt (PC/LAN, Router, Provider), damit ich z. B. einen Router-Neustart von einem Providerausfall unterscheide. | Muss |
| F-04 | Als Fahrer will ich alle Aussetzer in einer Protokolldatei finden, die ich auch in Excel öffnen kann, damit ich sie auswerten und im Team teilen kann. | Muss |
| F-05 | Als Fahrer will ich, dass NetWatch protokolliert, wann es selbst lief (Start, Ende, Standby, Ruhezustand), damit ich „keine Aussetzer“ von „nicht gemessen“ unterscheiden kann. | Muss |
| F-06 | Als Fahrer will ich ein Tray-Symbol mit Status (grün: verbunden, rot: Aussetzer läuft, grau: Messung pausiert), Hover-Info und Menü (Protokollordner öffnen, Beenden). | Muss |
| F-07 | Als Fahrer will ich im Tray-Menü einschalten können, dass NetWatch automatisch mit Windows startet, damit die Messung nicht von meinem Gedächtnis abhängt. Standard: aus. | Muss |
| F-08 | Als Fahrer will ich einen Tagesbericht mit 24-h-Zeitleiste und Ereignistabelle, damit ich einen Tag auf einen Blick sehe. | Soll |
| F-09 | Als Fahrer will ich einen Wochenbericht als Raster Wochentag × Uhrzeit, damit wiederkehrende Zeitpunkte wie 02:00 auffallen. | Soll |
| F-10 | Als Fahrer will ich zusätzliche Prüfziele (z. B. einen Sim-Server) einstellen können. | Kann |
| F-12 | Als Fahrer will ich die Empfindlichkeit wählen können (fein: 100 ms / Aussetzer ab ca. 0,2 s; normal: 200 ms / ab ca. 0,4 s; grob: 500 ms / ab ca. 1 s), damit ich Genauigkeit gegen Last abwägen kann. Standard: normal. | Soll |
| F-11 | Als Teamchef will ich die Protokolle mehrerer Fahrer übereinanderlegen (Team-Stabilitätskarte), damit ich Stints danach plane. | Kann (Später-Liste) |

## Ein- und Ausgaben

**Eingaben:** keine Dateien. NetWatch ermittelt das Standard-Gateway selbst; Einstellungen kommen aus einer optionalen `settings.json` neben dem Programm (Felder siehe Datenmodell in `architecture.md`).

**Ausgabe 1, Ereignisprotokoll** `netwatch_events.csv` (UTF-8, Semikolon, eine Zeile pro Ereignis). Beispiel mit erfundenen Werten:

```
start_local;end_local;start_utc;duration_ms;cause;failed_targets;gateway
2026-10-13T02:00:03.412+02:00;2026-10-13T02:00:05.214+02:00;2026-10-13T00:00:03.412Z;1802;router;192.168.178.1,1.1.1.1,8.8.8.8;192.168.178.1
2026-10-13T14:38:51.090+02:00;2026-10-13T14:42:56.120+02:00;2026-10-13T12:38:51.090Z;245030;provider;1.1.1.1,8.8.8.8;192.168.178.1
```

Ursachenwerte: `pc_lan` (Netzwerkadapter meldet getrennt), `router` (Router und Internetziele antworten nicht), `provider` (Router antwortet, Internetziele nicht).

**Ausgabe 2, Laufzeitprotokoll** `netwatch_sessions.csv`: Start, Ende und Grund (Programmstart, Beenden, Standby, Ruhezustand, Absturz erkannt beim nächsten Start).

Typische Größe: wenige Zeilen pro Tag, auch bei Dauerbetrieb über Monate unter 1 MB.

## Grenzfälle

- **Zeitumstellung:** Die Sommerzeit endet am 25.10.2026 um 03:00, also mitten im 24-h-Rennen. Deshalb werden immer UTC und Ortszeit mit Versatz gespeichert.
- **Standby oder Ruhezustand:** Der Sim-PC läuft während der Messphasen durch (Angabe von Nils); trotzdem gilt: Die Pause wird als Messlücke im Laufzeitprotokoll vermerkt, nicht als Aussetzer.
- **Aussetzer beim Beenden oder Absturz:** Beim nächsten Start wird ein offener Aussetzer mit Ende „unbekannt“ abgeschlossen.
- **Gateway ändert sich oder mehrere Adapter (z. B. VPN):** Das Gateway wird regelmäßig neu ermittelt; verwendet wird der Adapter mit der Standardroute.
- **Router beantwortet keine Pings:** NetWatch erkennt das beim Start und meldet, dass die Ursache dann nur „Internet weg“ heißen kann.
- **Protokolldatei gesperrt** (z. B. in Excel geöffnet): Einträge werden gepuffert und später geschrieben, nichts geht verloren.
- **Einzelne verlorene Pings** ohne echten Ausfall: Ein Aussetzer zählt erst ab einer Mindestzahl verlorener Prüfungen in Folge bei allen Internetzielen (Vorschlag: 2).
- **Sehr lange Ausfälle** (Stunden): werden normal als ein Ereignis protokolliert.
- **Falsche Systemuhr:** Annahme: Windows synchronisiert die Uhr automatisch; NetWatch korrigiert sie nicht.

## Nicht-funktionale Anforderungen

- Windows 10 und 11, 64 Bit.
- Eine einzelne .exe ohne Installation und ohne Administratorrechte.
- Läuft komplett offline-fähig und lokal; es werden keine Daten an Dritte gesendet (außer den Pings selbst).
- Geringe Last, damit die Sim nicht leidet (Annahme: unter 1 % CPU und unter 100 MB Arbeitsspeicher).
- Das Messen darf nicht ins Stocken geraten, während das Tray-Menü oder ein Bericht offen ist.
- Zeitstempel mit Millisekunden; Erkennungsgenauigkeit des Beginns innerhalb eines Prüftakts.
- Deutsche Oberfläche; Code, Dateinamen und CSV-Spalten auf Englisch.

## Nicht-Ziele

- Kein Zugriff auf Router oder Fritzbox, weder lesend noch steuernd.
- Keine Bandbreiten- oder Speedtests.
- Keine Reparatur oder Änderung von Netzwerkeinstellungen.
- Kein Cloud-Dienst und keine zentrale Teamdatenbank im MVP.
- Keine Benachrichtigung und kein Ton bei Aussetzern.
- Keine Latenz- oder Jitter-Analyse zum Spielserver im MVP.
- Kein macOS oder Linux.

## Akzeptanzkriterien der Muss-Anforderungen

| ID | Testfall | Erwartetes Ergebnis |
| --- | --- | --- |
| F-01 | NetWatch 10 Minuten laufen lassen, Prüfungen im Debug-Protokoll zählen. | Pro Ziel 3.000 ± 1 % Prüfungen bei 200 ms Takt. |
| F-02 | Automatischer Test mit simuliertem Prüfer: Ausfall aller Internetziele von 12:00:00,000 bis 12:00:01,500. | Genau ein Aussetzer, Beginn 12:00:00,000 ± 1 Takt, Dauer 1.500 ms ± 1 Takt. |
| F-02 | Manuell: LAN-Kabel 5 s ziehen und Zeit mit Stoppuhr notieren. | Ein Aussetzer, Beginn auf die Sekunde mit der notierten Zeit übereinstimmend, Dauer 5 s ± 1 s. |
| F-02 | Automatischer Test: einzelne verlorene Prüfung an nur einem Ziel. | Kein Aussetzer. |
| F-03 | Simulierter Prüfer: Router und Internet aus / nur Internet aus / Adapter getrennt. | Ursache `router` / `provider` / `pc_lan`. |
| F-03 | Manuell: Router neu starten. | Ein Aussetzer mit Ursache `router`. |
| F-04 | Ereignisprotokoll in Excel öffnen. | Spalten wie oben, Umlaute korrekt, eine Zeile pro Aussetzer. |
| F-04 | Automatischer Test: Aussetzer über die Zeitumstellung 25.10.2026 02:59:59 MESZ bis 02:00:01 MEZ. | Dauer 2 s, nicht 1 h 0 min 2 s; UTC-Werte korrekt. |
| F-05 | PC in Standby schicken und aufwecken. | Laufzeitprotokoll zeigt die Pause; kein Aussetzer für die Standby-Zeit. |
| F-06 | Kabel ziehen und wieder stecken. | Symbol wird binnen 1 s rot, danach wieder grün; Hover zeigt den letzten Aussetzer. |
| F-07 | Autostart im Tray-Menü einschalten, Windows neu starten; danach ausschalten und erneut neu starten. | Eingeschaltet: NetWatch läuft nach der Anmeldung ohne Zutun. Ausgeschaltet: NetWatch startet nicht. |

## Testdaten für Claude Code

- Keine echten Messdaten vorhanden (bewusst entschieden, siehe Entscheidungslog).
- Automatische Tests verwenden einen simulierten Prüfer mit festgelegten Ausfallmustern (Router-Neustart, Providerausfall, einzelne Paketverluste, Zeitumstellung).
- Manuelle Tests erzeugen echte Daten: LAN-Kabel ziehen, Router neu starten, Netzwerkadapter deaktivieren. Die entstehenden CSV-Dateien kommen nach `testdata/`.
- Anonymisierung ist nicht nötig, weil nur private Router-IPs und öffentliche DNS-Adressen vorkommen.
