# NetWatch

NetWatch überwacht im Hintergrund deine Internetverbindung und schreibt jeden Aussetzer mit Uhrzeit, Dauer und Ursache in eine Datei. Gedacht ist es für Simracing-Langstreckenrennen: Lass NetWatch einige Tage vor dem Rennen laufen, dann siehst du, wann deine Leitung kritisch ist, zum Beispiel bei einem automatischen Router-Neustart um 02:00.

## Für Fahrer: Installation und Bedienung

1. `NetWatch.exe` aus dem ZIP in einen eigenen, festen Ordner legen, zum Beispiel `C:\Tools\NetWatch\`. Eine Installation ist nicht nötig, Adminrechte auch nicht.
2. Doppelklick auf `NetWatch.exe`. Weil das Programm nicht digital signiert ist, zeigt Windows beim ersten Start „Der Computer wurde durch Windows geschützt“. Klicke auf **Weitere Informationen** und dann auf **Trotzdem ausführen**.
3. Neben der Uhr erscheint das NetWatch-Symbol (eventuell unter dem Pfeil „Ausgeblendete Symbole“):
   - grün: Verbindung besteht
   - rot: gerade läuft ein Aussetzer
   - grau: noch keine gültige Messung, z. B. in den ersten Sekunden nach dem Start oder nach dem Aufwachen aus dem Standby
4. Fährst du mit der Maus über das Symbol, siehst du, seit wann die Verbindung besteht und wann der letzte Aussetzer war (Uhrzeit, Dauer, Ursache).
5. Rechtsklick auf das Symbol öffnet das Menü:
   - **Protokollordner öffnen:** zeigt die Protokolldateien
   - **Autostart:** NetWatch startet mit Windows (standardmäßig aus). Lege die .exe *vorher* in ihren festen Ordner: Gespeichert wird der aktuelle Pfad. Verschiebst du die .exe später, schalte den Autostart einmal aus und wieder ein.
   - **Beenden**
6. NetWatch läuft nur einmal. Startest du es ein zweites Mal, erscheint der Hinweis „NetWatch läuft bereits“.
7. Lass den PC während der Messung eingeschaltet. Ist er im Standby, wird nicht gemessen; diese Zeit steht als Messlücke im Laufzeitprotokoll.
8. NetWatch meldet sich bei Aussetzern bewusst nicht mit Ton oder Benachrichtigung, damit es im Rennen nicht stört.

### Protokolldateien

Die Dateien liegen in `%LOCALAPPDATA%\NetWatch\` und lassen sich mit Excel öffnen:

- `netwatch_events.csv`: ein Eintrag pro Aussetzer mit Beginn, Ende, Dauer in Millisekunden und Ursache
  - `pc_lan`: Netzwerkkabel oder Netzwerkkarte getrennt
  - `router`: auch der Router antwortet nicht, typisch für einen Router-Neustart
  - `provider`: der Router antwortet, das Internet dahinter nicht
  - `unknown`: nicht eingrenzbar, weil dein Router grundsätzlich keine Pings beantwortet
- `netwatch_sessions.csv`: wann NetWatch gemessen hat und wann nicht (Start, Beenden, Standby, Aufwachen, Herunterfahren, Absturz)

Ist die Datei gerade in Excel geöffnet, merkt sich NetWatch neue Einträge und schreibt sie, sobald du Excel schließt.

Wichtig: NetWatch erkennt Aussetzer ab etwa 0,4 Sekunden. Noch kürzere Lücken fallen zwischen zwei Prüfungen durch.

Weitere Dateien im Ordner: `state.json` (Zustand für die Absturzerkennung, wird beim Beenden gelöscht) und gegebenenfalls `netwatch_errors.log` (unerwartete Fehler; bitte bei Problemen mitschicken).

## Für Entwickler

Voraussetzung: Windows 10/11 und das .NET 10 SDK (zum Beispiel per `winget install Microsoft.DotNet.SDK.10`). Die SDK-Version und die Testplattform sind in `global.json` festgelegt.

- Einrichten: `dotnet restore`
- Starten: `dotnet run --project src/NetWatch.App`
- Tests: `dotnet test`
- Diagnose (pingt Gateway, 1.1.1.1 und 8.8.8.8 im 200-ms-Takt und gibt eine Auswertung aus): `dotnet run --project src/NetWatch.App -- --diagnose 600`. Mit der veröffentlichten .exe: `.\NetWatch.exe --diagnose 600 | Out-Host`, damit PowerShell auf das Ende wartet. Der Bericht landet zusätzlich in `%LOCALAPPDATA%\NetWatch\diagnose_<Datum>_<Uhrzeit>.txt`.
- Veröffentlichen als einzelne .exe: `dotnet publish src/NetWatch.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

Details stehen in `docs/`, die Arbeitsregeln für Claude Code in `CLAUDE.md`.
