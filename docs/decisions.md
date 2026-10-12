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
| 12.10.2026 | M0: Solution im klassischen Format `NetWatch.sln` (nicht `.slnx`) | So in der Definition of Done von M0 festgelegt; .NET 10 würde standardmäßig `.slnx` anlegen |
| 12.10.2026 | M0: `global.json` legt das SDK auf 10.0.401 fest (neuere Feature-Versionen erlaubt) und schaltet für `dotnet test` die Testplattform Microsoft.Testing.Platform ein | Alle bauen mit derselben SDK-Linie; xUnit v3 läuft unter .NET 10 nur noch mit der neuen Testplattform |
| 12.10.2026 | M0: Einzige Testabhängigkeit ist `xunit.v3` 4.0.2 (Apache-2.0); bringt Microsoft.Testing.Platform (MIT) mit. `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` und `coverlet.collector` entfallen | Werden mit der neuen Testplattform nicht gebraucht; weniger Abhängigkeiten |
| 12.10.2026 | M0: `Directory.Build.props` mit Nullable, ImplicitUsings und „Warnungen gelten als Fehler“ für alle Projekte | Einheitliche, strenge Einstellungen von Anfang an |
| 12.10.2026 | M0: Tray-Symbol wird zur Laufzeit als farbiger Punkt gezeichnet (grau/grün/rot), keine Icon-Dateien | Keine Grafikdateien oder Abhängigkeiten; Farben im Code änderbar |
| 12.10.2026 | M0: Programmname (AssemblyName) `NetWatch`, damit die Datei `NetWatch.exe` heißt | Passt zur README |
| 12.10.2026 | Ursachenwechsel innerhalb eines Aussetzers: ein Ereignis, Ursache = am weitesten vorne liegende Ursache, die währenddessen auftrat (`pc_lan` vor `router` vor `provider`) | Ein Router-Neustart (erst `router`, dann `provider` während der DSL-Synchronisation) bleibt ein Ereignis mit Ursache `router`; von Nils freigegeben (F4) |
| 12.10.2026 | Prüfrunden: alle Ziele werden im selben Takt gepingt. Aussetzer = 2 Runden in Folge ohne Antwort eines Internetziels; Beginn = Sendezeit der ersten dieser Runden, Ende = Sendezeit der ersten Runde mit Antwort | Macht „in Folge bei allen Internetzielen“ eindeutig; von Nils freigegeben (F5) |
| 12.10.2026 | Ping-Zeitlimit bleibt kleiner als der Prüftakt; eine spätere Antwort zählt als Verlust | Wie in `architecture.md` vorgesehen; von Nils freigegeben (F6) |
| 12.10.2026 | Laufzeitprotokoll: Startgründe `start`, `resume`; Endgründe `exit`, `suspend`, `shutdown`, `crash_detected`; immer UTC und Ortszeit. Ein laufender Aussetzer endet beim Suspend. Eine Zustandsdatei wird jede Minute aktualisiert, damit nach einem Absturz Sitzungs- und Aussetzerende geschätzt werden können | Windows unterscheidet Standby und Ruhezustand über `SystemEvents` nicht; Herunterfahren soll nicht wie ein Absturz aussehen; von Nils freigegeben (F7) |
| 12.10.2026 | Ereignisprotokoll: zusätzliche Spalten `end_utc` und `note` (z. B. `end_unknown`); CSV als UTF-8 mit BOM | Regel „immer UTC plus Ortszeit“; Excel erkennt UTF-8 nur mit BOM; von Nils freigegeben (F8) |
| 12.10.2026 | `settings.json` liegt in `%LOCALAPPDATA%\NetWatch\`; `Sensitivity` bestimmt den Prüftakt, `IntervalMs` entfällt | Der Ordner der .exe ist nicht immer beschreibbar; keine doppelte Einstellung; von Nils freigegeben (F9) |
| 12.10.2026 | „Debug-Protokoll“ im Akzeptanztest F-01 meint den Modus `--diagnose`; kein dauerhaftes Protokoll jeder Prüfung | Ein Protokoll jeder Prüfung wären rund 1,3 Mio. Zeilen pro Tag; von Nils freigegeben (F10) |
| 12.10.2026 | Graues Symbol = keine gültige Messung (Start bis zur ersten Prüfrunde, direkt nach dem Aufwachen); keine Pause-Funktion | Es gibt keinen Menüpunkt „Pause“; von Nils freigegeben (F11) |
| 12.10.2026 | „Router beantwortet keine Pings“ erscheint im Hover-Text und als Hinweis im Laufzeitprotokoll, nicht als Benachrichtigung | Nicht-Ziel „keine Benachrichtigung“; von Nils freigegeben (F12) |
| 12.10.2026 | Schutz gegen doppelten Start kommt in M4 | Autostart und Doppelklick dürfen nicht zwei Instanzen in dieselbe CSV schreiben lassen; von Nils freigegeben (F13) |
| 12.10.2026 | `requirements.md` und `architecture.md` werden an die Entscheidungen F4 bis F13 angepasst, wenn der jeweilige Meilenstein (M2 bis M4) umgesetzt wird | Bis dahin gilt bei Widersprüchen dieses Entscheidungslog |
