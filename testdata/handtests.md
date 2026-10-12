# Handtests

Von Hand notierte Uhrzeiten zu den Handtests, zum Vergleich mit den erkannten Zeiten.

| Datum | Test | Notierte Uhrzeit (Ortszeit) | Datei | Erkannt |
| --- | --- | --- | --- | --- |
| 12.10.2026 | LAN-Kabel ca. 5 s gezogen (während `--diagnose 90`) | Ziehen 15:36:24 | `2026-10-12_kabel-5s_diagnose.txt` | Erste verlorene Runde 15:36:25,6 (Logzeit); Adapter „Down“ 15:36:25,7 bis „Up“ 15:36:36,2; Internet wieder ab ca. 15:36:35,8 |
| 12.10.2026 | Standby ca. 80 s (veröffentlichte .exe 0.1.0) | – | `2026-10-12_standby_power.log` | `suspend` 15:43:51,2, `resume` 15:45:12,1; Systemuhr 80.871 ms, Stopwatch 81.065 ms |
| 12.10.2026 | M3: Absturz (NetWatch nach 8 s hart beendet, neu gestartet) | – | `2026-10-12_m3_sessions.csv` | Zeile `start` → `crash_detected`, Hinweis `end_estimated` |
| 12.10.2026 | M3: LAN-Kabel ca. 5 s gezogen (NetWatch läuft, parallel `--diagnose 90`) | Ziehen 16:32:27 | `2026-10-12_m3_events.csv`, `2026-10-12_m3_kabel-5s_diagnose.txt` | `pc_lan` 16:32:29,357 bis 16:32:39,761 (10.404 ms); Adapter „Down“ 16:32:30,080 bis „Up“ 16:32:40,659 (10.579 ms). Längste Runde 189 ms (eigenes Zeitlimit) |
| 12.10.2026 | M3: Standby ca. 70 s | – | `2026-10-12_m3_sessions.csv` | Phase endet 16:35:38 mit `suspend`, neue Phase 16:36:46 mit `resume`; kein Aussetzer für Standby und Aufwachen |
| 12.10.2026 | M3: CSV in Excel geöffnet (gesperrt), Kabel ca. 3 s gezogen, Excel geschlossen | – | `2026-10-12_m3_events.csv` | `pc_lan` 16:42:44,013 (8.790 ms) gepuffert und um 16:43:17 nach dem Schließen geschrieben |
| – | M3: Router-Neustart (F-03 manuell) | – | – | **Übersprungen**: kein Zugriff auf die Fritzbox; offen |
| 12.10.2026 | M4: Kabel ca. 5 s gezogen (0.2.0 aus `C:\Tools\NetWatch\`, synchroner Prober) | – | `netwatch_events.csv` (Messdaten-Ordner) | Symbol rot, danach grün; Hover zeigt den Aussetzer; `pc_lan` 17:38:35,757 (6.399 ms) |
| 12.10.2026 | M4: Menü offen, währenddessen Kabel zweimal gezogen | – | dto. | `pc_lan` 17:39:21,547 (11.800 ms) und 17:39:45,758 (15.396 ms); Messung lief bei offenem Menü weiter |
| 12.10.2026 | M4: Zweiter Start, Protokollordner öffnen | – | – | Hinweis „NetWatch läuft bereits“, nur ein Symbol; Explorer zeigt den Ordner |
| 12.10.2026 | M4: Dauerlauf 2 h (0.2.0, Release-.exe) | – | `2026-10-12_m4_dauerlauf-2h.csv` | Arbeitsspeicher höchstens 83,1 MB, privat 16–22 MB, Handles 439–661 stabil, CPU ca. 0,03 % |
| 12.10.2026 | M4: F-07 Autostart an, Neustart (0.2.1) | – | `2026-10-12_m4_sessions_echt.csv` | NetWatch 21:32:08 von Windows gestartet (Shell-Core-Ereignis 9707); Phase davor endet 21:30:13 mit `shutdown` |
| 12.10.2026 | M4: F-07 Autostart aus, Neustart | – | dto. | NetWatch nicht gestartet; Phase davor endet 21:36:35 mit `shutdown` |
| 12.10.2026 | M4: Messstart (0.2.2, von Nils per Doppelklick, Autostart an) | 21:47:03 | – | läuft; `crash_detected` für 21:39:13 (von Claude hart beendete Instanz) |
