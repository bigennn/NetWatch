# NetWatch

NetWatch überwacht im Hintergrund deine Internetverbindung und schreibt jeden Aussetzer mit Uhrzeit, Dauer und Ursache in eine Datei. Gedacht ist es für Simracing-Langstreckenrennen: Lass NetWatch einige Tage vor dem Rennen laufen, dann siehst du, wann deine Leitung kritisch ist, zum Beispiel bei einem automatischen Router-Neustart um 02:00.

## Für Fahrer: Installation und Bedienung

1. `NetWatch.exe` herunterladen und in einen eigenen Ordner legen, zum Beispiel `C:\Tools\NetWatch\`. Eine Installation ist nicht nötig, Adminrechte auch nicht.
2. Doppelklick auf `NetWatch.exe`. Weil das Programm nicht digital signiert ist, zeigt Windows beim ersten Start „Der Computer wurde durch Windows geschützt“. Klicke auf **Weitere Informationen** und dann auf **Trotzdem ausführen**.
3. Neben der Uhr erscheint das NetWatch-Symbol (eventuell unter dem Pfeil „Ausgeblendete Symbole“):
   - grün: Verbindung besteht
   - rot: gerade läuft ein Aussetzer
   - grau: Messung pausiert
4. Rechtsklick auf das Symbol öffnet das Menü:
   - **Protokollordner öffnen:** zeigt die Protokolldateien
   - **Autostart:** NetWatch startet mit Windows (standardmäßig aus)
   - **Beenden**
5. Lass den PC während der Messung eingeschaltet. Ist er im Standby, wird nicht gemessen; diese Zeit steht als Messlücke im Laufzeitprotokoll.

### Protokolldateien

Die Dateien liegen in `%LOCALAPPDATA%\NetWatch\` und lassen sich mit Excel öffnen:

- `netwatch_events.csv`: ein Eintrag pro Aussetzer mit Beginn, Ende, Dauer in Millisekunden und Ursache
  - `pc_lan`: Netzwerkkabel oder Netzwerkkarte getrennt
  - `router`: auch der Router antwortet nicht, typisch für einen Router-Neustart
  - `provider`: der Router antwortet, das Internet dahinter nicht
- `netwatch_sessions.csv`: wann NetWatch gemessen hat und wann nicht

Wichtig: NetWatch erkennt Aussetzer ab etwa 0,4 Sekunden. Noch kürzere Lücken fallen zwischen zwei Prüfungen durch.

## Für Entwickler

Voraussetzung: Windows 10/11 und das .NET 10 SDK (zum Beispiel per `winget install Microsoft.DotNet.SDK.10`). Die SDK-Version und die Testplattform sind in `global.json` festgelegt.

- Einrichten: `dotnet restore`
- Starten: `dotnet run --project src/NetWatch.App`
- Tests: `dotnet test`
- Veröffentlichen als einzelne .exe: `dotnet publish src/NetWatch.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

Details stehen in `docs/`, die Arbeitsregeln für Claude Code in `CLAUDE.md`.
