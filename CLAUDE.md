# NetWatch

NetWatch ist eine Windows-Tray-App, die dauerhaft im Hintergrund prüft, ob die Internetverbindung besteht, und jeden Aussetzer, auch unter einer Sekunde, mit Beginn, Dauer und Ursache (PC/LAN, Router, Provider) protokolliert. So lassen sich vor Simracing-Langstreckenrennen kritische Zeitpunkte wie automatische Router-Neustarts erkennen und im Rennplan berücksichtigen.

## Tech-Stack
- C# mit .NET 10 (LTS), WinForms für das Tray-Symbol
- Ping über `System.Net.NetworkInformation.Ping` (ohne Adminrechte), Energieereignisse über `Microsoft.Win32.SystemEvents`
- Tests: xUnit
- Keine weiteren Laufzeitabhängigkeiten

## Befehle
- Einrichten: `dotnet restore`
- Starten: `dotnet run --project src/NetWatch.App`
- Tests: `dotnet test`
- Diagnose (ab M1): `dotnet run --project src/NetWatch.App -- --diagnose 600`
- Veröffentlichen: `dotnet publish src/NetWatch.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

## Projektstruktur
- `src/NetWatch.Core/`: reine Logik ohne Windows-Abhängigkeit (Detektor, Ursachenzuordnung, CSV-Format, Berichte); Uhr und Prüfer werden als Schnittstellen übergeben
- `src/NetWatch.App/`: Windows-Teile (Tray, Ping, Gateway, Energieereignisse, Autostart), Einstiegspunkt
- `tests/NetWatch.Core.Tests/`: xUnit-Tests mit simulierter Uhr und simuliertem Prüfer
- `docs/`: Steckbrief, Anforderungen, Architektur, Roadmap, Entscheidungslog
- `testdata/`: Protokolle aus Handtests (siehe `testdata/README.md`)

## Arbeitsregeln
- Arbeite die Meilensteine aus docs/roadmap.md der Reihe nach ab.
- Beginne jeden Meilenstein mit einem Plan und warte auf meine Freigabe.
- Jede Muss-Anforderung bekommt einen Test zu ihrem Akzeptanzkriterium.
- Neue Abhängigkeiten nur nach Rückfrage und mit Lizenzangabe.
- Architekturentscheidungen trägst du in docs/decisions.md ein.
- Nutze nur Daten aus testdata/; committe keine echten Firmendaten.
- Nach jedem Meilenstein: Tests grün, Status in docs/roadmap.md aktualisieren, committen.
- Einheiten: Zeiten und Dauern in Millisekunden; Zeitstempel ISO 8601 mit Millisekunden, immer UTC plus Ortszeit mit Versatz. Dauern immer aus einer monotonen Uhr (`Stopwatch`), nie aus der Differenz zweier Systemzeiten.
- C# ist für mich neu: Erkläre neue Sprachkonzepte kurz, wenn sie zum ersten Mal vorkommen, und fasse nach jedem Meilenstein in wenigen Sätzen zusammen, was ich daraus lernen kann.
- Die Messung darf nie blockieren: Kein Datei- oder UI-Zugriff im Takt-Pfad des Prüfers.
- Oberfläche auf Deutsch; Code, Dateinamen und CSV-Spalten auf Englisch.

## Dokumente
- docs/brief.md: Problem und MVP-Kern
- docs/requirements.md: Anforderungen mit Akzeptanzkriterien
- docs/architecture.md: Aufbau und Datenmodell
- docs/roadmap.md: Meilensteine und Status
- docs/decisions.md: Entscheidungslog
