# Entscheidungslog: NetWatch

| Datum | Entscheidung | Begründung |
| --- | --- | --- |
| 12.10.2026 | Kurzname NetWatch, Kategorie Hobby, Lernprojekt | Von Nils gewählt |
| 12.10.2026 | Lösungsvariante B: Windows-Tray-App als einzelne .exe | Einfach an das Team zu verteilen; alle Nutzer haben Windows |
| 12.10.2026 | Beispieldaten bewusst übersprungen | Es gibt noch keine aufgezeichneten Aussetzer. Testdaten werden später selbst erzeugt (LAN-Kabel ziehen, Router neu starten). Risiko: Das Protokollformat wird ohne echte Daten entworfen. |
| 12.10.2026 | Stilles Protokoll, keine Benachrichtigung und kein Ton | Würde im Rennen stören |
| 12.10.2026 | Wochenbericht (Raster Wochentag × Uhrzeit) aufgenommen | Macht wiederkehrende Zeitpunkte wie 02:00 sofort sichtbar |
| 12.10.2026 | Selbst bauen statt PingPlotter oder Internet-Check | Kein vorhandenes Tool erkennt Sub-Sekunden-Aussetzer, grenzt die Ursache ein und erzeugt einen Wochenbericht; PingPlotter bräuchte Lizenzen fürs Team |
| 12.10.2026 | MVP-Kern: lückenloses Protokoll mit Beginn, Dauer und Ursache | Berichte bauen darauf auf und folgen danach |
| 12.10.2026 | Gate 1 bestätigt | Steckbrief von Nils freigegeben |
| 12.10.2026 | Phasen 2 und 3 kompakt | Hobby-Tool und Zeitdruck bis zum Rennen am 24./25.10.2026 |
| 12.10.2026 | Autostart als einschaltbare Option (Standard aus) | Nils: „Autostart sollte möglich sein“ |
| 12.10.2026 | Prüftakt 200 ms, Aussetzer ab 2 verlorenen Prüfungen; Empfindlichkeit wählbar als Soll (F-12) | Guter Kompromiss aus Genauigkeit und Last; Feineinstellung gewünscht, aber nicht MVP-kritisch |
| 12.10.2026 | Keine Standby-Verhinderung nötig | Sim-PC läuft in Messphasen durch |
| 12.10.2026 | Gate 2 bestätigt | Anforderungen von Nils freigegeben |
| 12.10.2026 | Stack: C# mit .NET 10 und WinForms | Tray, Ping, Energieereignisse und Autostart sind enthalten; keine Laufzeitabhängigkeiten; weniger Virenscanner-Fehlalarme als PyInstaller; .NET 10 mit Unterstützung bis 11/2028. Nils kennt weder C# noch Python, C# ist Lernsprache. |
| 12.10.2026 | Claude Code läuft lokal auf Nils' Windows-PC | Windows-spezifische Teile (Ping, Tray, Standby) lassen sich direkt testen |
| 12.10.2026 | Keine Code-Signatur; SmartScreen-Warnung akzeptiert | Kosten sparen; Team kann „Trotzdem ausführen“ wählen |
| 12.10.2026 | Gate 3 bestätigt | Konzept von Nils freigegeben |
| 12.10.2026 | Wochenbericht (M5) vor Tagesbericht (M6) geplant | Für die Rennplanung zählt das wiederkehrende Muster; von Nils bestätigt |
| 12.10.2026 | Gate 4 bestätigt: ein Meilenstein pro Abend bis 17.10., Spike S4 mit einem Teamkollegen bis 14.10. | Messstart rechtzeitig vor dem Rennen am 24./25.10.2026 |
| 12.10.2026 | Testdaten: keine echten Messdaten im Paket; testdata/ enthält nur README und wird durch Handtests befüllt | Bewusst entschieden in Phase 1, siehe oben |
