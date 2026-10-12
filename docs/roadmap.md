# Roadmap: NetWatch

Stand: 12.10.2026. Zieltermine sind Planwerte (Annahme), kein Versprechen.

Zeitliche Leitplanke: Bis etwa Samstag, 17.10.2026, soll NetWatch (M0 bis M4) dauerhaft auf Nils' PC laufen. Dann liegen bis zum 24-h-Rennen am 24./25.10.2026 rund sieben Tage Messdaten vor. Die Berichte (M5, M6) werten bereits gesammelte CSV-Dateien aus und dürfen deshalb nach dem Messstart entstehen.

## M0: Projektgerüst
- Ziel: Eine leere Tray-App startet, zeigt ein graues Symbol und lässt sich über das Menü beenden; ein erster Test ist grün.
- Anforderungen: keine (Grundlage)
- Definition of Done: Solution `NetWatch.sln` mit `src/NetWatch.Core` (net10.0, reine Logik), `src/NetWatch.App` (net10.0-windows, WinForms) und `tests/NetWatch.Core.Tests` (xUnit). `dotnet build` und `dotnet test` laufen fehlerfrei, ein Platzhaltertest ist grün. README enthält Einrichten, Starten und Testen.
- So teste ich es selbst: `dotnet run --project src/NetWatch.App` ausführen, graues Symbol neben der Uhr suchen, Rechtsklick, „Beenden“.
- Status: erledigt am 12.10.2026 (Build ohne Warnungen, 1 Test grün)
- Zieltermin: 13.10.2026

## M1: Spikes S1 bis S4
- Ziel: Die vier technischen Risiken aus `architecture.md` sind geprüft, bevor die Fachlogik entsteht.
- Anforderungen: Vorbereitung für F-01, F-03, F-05, Auslieferung
- Definition of Done:
  - S1: Eine Diagnose-Messung (`NetWatch.App --diagnose 600`) pingt Gateway, 1.1.1.1 und 8.8.8.8 zehn Minuten im 200-ms-Takt und gibt aus: Prüfungen je Ziel, Verluste, größter Abstand zwischen zwei Prüfungen, mittlere CPU-Last. Erwartung: 3.000 ± 1 % Prüfungen je Ziel ohne Adminrechte.
  - S2: Standby und Aufwachen werden mit Uhrzeit in eine Diagnosedatei geschrieben.
  - S3: Das Gateway wird automatisch ermittelt und beantwortet Pings (Ergebnis wird ausgegeben).
  - S4: Die M0-App ist als self-contained Single-File-.exe veröffentlicht und auf einem zweiten PC gestartet worden; Verhalten von SmartScreen und Virenscanner ist notiert.
  - Ergebnisse und Folgerungen stehen in `docs/decisions.md`.
- So teste ich es selbst: Diagnose 10 Minuten laufen lassen und Ausgabe lesen; PC in Standby schicken und aufwecken; .exe einem Teamkollegen schicken.
- Status: offen
- Zieltermin: 14.10.2026

## M2: Erkennung und Ursache (MVP-Kern, reine Logik)
- Ziel: Der Aussetzer-Detektor erkennt aus Prüfergebnissen jeden Aussetzer mit Beginn, Ende, Dauer und Ursache.
- Anforderungen: F-02, F-03
- Definition of Done: `OutageDetector` und Ursachenzuordnung in `NetWatch.Core`, ohne Windows-Abhängigkeit, mit simulierter Uhr und simuliertem Prüfer getestet. Grüne Tests für alle Akzeptanzkriterien von F-02 und F-03 aus `requirements.md` (1,5-s-Ausfall, einzelner Paketverlust, `router`, `provider`, `pc_lan`).
- So teste ich es selbst: `dotnet test` ausführen und die Testnamen lesen; Claude Code erklärt jeden Testfall in einem Satz.
- Status: offen
- Zieltermin: 15.10.2026

## M3: Protokolle und Messbetrieb ohne Oberfläche
- Ziel: NetWatch misst echt und schreibt Ereignis- und Laufzeitprotokoll.
- Anforderungen: F-01, F-04, F-05
- Definition of Done: Prober, GatewayResolver, EventLogWriter und SessionTracker sind verbunden. CSV-Dateien in `%LOCALAPPDATA%\NetWatch\` im Format aus `requirements.md`. Grüne Tests für CSV-Format, Zeitumstellung am 25.10.2026, gesperrte Datei (Puffer) und Absturzerkennung. Akzeptanztest F-01 (10 Minuten, Prüfungen zählen) bestanden.
- So teste ich es selbst: Messung starten, LAN-Kabel 5 s ziehen und Uhrzeit notieren, `netwatch_events.csv` in Excel öffnen und vergleichen; PC in Standby schicken und `netwatch_sessions.csv` prüfen.
- Status: offen
- Zieltermin: 16.10.2026

## M4: Tray-App, Autostart und Dauerbetrieb
- Ziel: NetWatch läuft als fertige .exe dauerhaft im Hintergrund.
- Anforderungen: F-06, F-07
- Definition of Done: Symbol grün/rot/grau mit Hover-Text (Status, Verbindungsdauer, letzter Aussetzer); Menü „Protokollordner öffnen“, „Autostart“ (an/aus, Standard aus), „Beenden“. Messen stockt nicht, während das Menü offen ist. Single-File-.exe veröffentlicht. README für Teamkollegen mit SmartScreen-Hinweis. Manuelle Akzeptanztests F-06 und F-07 bestanden.
- So teste ich es selbst: .exe starten, Kabel ziehen (Symbol wird binnen 1 s rot), Autostart einschalten, Windows neu starten, prüfen, dass NetWatch läuft. Danach NetWatch bis zum Rennen durchlaufen lassen.
- Status: offen
- Zieltermin: 17.10.2026 (Messstart)

## M5: Wochenbericht
- Ziel: Ein HTML-Bericht zeigt alle Aussetzer der letzten sieben Tage als Raster Wochentag × Uhrzeit und markiert Zeiten ohne Messung.
- Anforderungen: F-09
- Definition of Done: Tray-Menü „Wochenbericht öffnen“ erzeugt `report_week.html` aus den CSV-Dateien und öffnet ihn im Browser. SVG selbst gezeichnet, keine Diagrammbibliothek. Tests mit erzeugten Testdaten (z. B. täglicher Aussetzer um 02:00) sind grün.
- So teste ich es selbst: Wochenbericht über das Menü öffnen und prüfen, ob die eigenen Kabeltests an der richtigen Stelle erscheinen.
- Status: offen
- Zieltermin: 20.10.2026

## M6: Tagesbericht
- Ziel: Ein HTML-Bericht zeigt einen Tag als 24-h-Zeitleiste mit Ereignistabelle und Verfügbarkeit.
- Anforderungen: F-08
- Definition of Done: Tray-Menü „Tagesbericht öffnen“ mit Tagesauswahl; Zeitleiste, Tabelle (Beginn, Dauer, Ursache), Verfügbarkeit in Prozent, Messlücken grau. Tests grün.
- So teste ich es selbst: Tagesbericht für einen Tag mit eigenem Kabeltest öffnen und mit der notierten Uhrzeit vergleichen.
- Status: offen
- Zieltermin: 22.10.2026

## M7: Einstellbare Empfindlichkeit
- Ziel: Die Empfindlichkeit lässt sich im Tray-Menü wählen.
- Anforderungen: F-12
- Definition of Done: Menü „Empfindlichkeit“ mit fein (100 ms), normal (200 ms, Standard), grob (500 ms); Auswahl wird in `settings.json` gespeichert und wirkt ohne Neustart. Tests grün.
- So teste ich es selbst: Auf „fein“ umstellen und mit der Diagnose prüfen, dass jetzt rund 6.000 Prüfungen in 10 Minuten erfolgen.
- Status: offen
- Zieltermin: nach dem Rennen

## Später-Liste (nicht geplant)
- F-10: zusätzliche Prüfziele, z. B. Sim-Server
- F-11: Team-Stabilitätskarte
