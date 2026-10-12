# Ideen-Steckbrief: NetWatch

- Kategorie: Hobby (Lernprojekt: Claude Code soll mehr erklären, der Stack darf neue Technik enthalten)
- Problem: Bei Langstreckenrennen im Simracing (aktuell Le Mans Ultimate) führen schon sehr kurze Verbindungsaussetzer zum Rauswurf vom Server. Weder Windows noch Discord zeigen solche Aussetzer an, deshalb bleiben Zeitpunkte und Ursachen wie ein automatischer Router-Neustart bis zum Rennen unbekannt.
- Heutiges Vorgehen und Aufwand: Kein Werkzeug im Einsatz. Aussetzer werden erst bemerkt, wenn man im Rennen vom Server fliegt. Bekannte Fälle: Router-Neustart täglich um 02:00 in einer früheren Wohnung (von Windows und Discord unbemerkt, nur durch den Server-Rauswurf aufgefallen) und unregelmäßige Ausfälle bis etwa 10 Minuten in einer anderen Wohnung (Fritzbox blinkt, Windows meldet „Verbunden, kein Internetzugriff“). In der aktuellen Wohnung ist bisher kein Abbruch aufgetreten.
- Nutzer: ich; später das ganze Simracing-Team (alle mit Windows)
- Gewählte Lösungsvariante: B, eine Windows-Tray-App als einzelne ausführbare Datei. Sie läuft dauerhaft im Hintergrund, protokolliert still (ohne Benachrichtigung oder Ton) und enthält einen Tages- und einen Wochenbericht. Grund: Teamkollegen können sie ohne Installation von Zusatzsoftware nutzen.
- MVP-Kern: NetWatch protokolliert jeden Verbindungsaussetzer lückenlos mit Beginn, Dauer und eingegrenzter Ursache (PC/LAN, Router oder Provider).
- Erfolgskriterium: (1) Der Zeitpunkt des automatischen Router-Neustarts wird sekundengenau und reproduzierbar erkannt. (2) Das 24-h-Rennen am 24./25.10.2026 wird mit Hilfe von NetWatch geplant und erfolgreich beendet.
- Vorhandene Alternativen: geprüft am 12.10.2026. PingPlotter Free speichert nur 10 Minuten Verlauf und prüft nur ein Ziel. PingPlotter Standard (ab 6,99 $/Monat, zwei Ziele, 1-s-Takt) wäre nutzbar, bräuchte aber eine Lizenz pro Teammitglied. Internet-Check (MIT, Windows-Tray) prüft frühestens alle 5 s und übersieht kurze Aussetzer. Kein gefundenes Tool vereint Sub-Sekunden-Erkennung, Eingrenzung der Ursache und Wochenbericht. Ergebnis: selbst bauen; eine PingPlotter-Testversion dient später als Vergleichsmessung.
- Später, nicht im MVP:
  - Tagesbericht (Zeitleiste und Ereignistabelle) – direkt nach dem MVP-Kern
  - Wochenbericht als Raster Wochentag × Uhrzeit – direkt nach dem MVP-Kern
  - Team-Stabilitätskarte: Protokolle mehrerer Teammitglieder übereinanderlegen, um Stints zu planen
  - Benachrichtigung oder Ton bei Aussetzern (bewusst nicht gewünscht, da es im Rennen stört)
- Offene Fragen (Stand Phase 5):
  - Geklärt in Phase 2: Prüftakt 200 ms, Aussetzer ab 2 verlorenen Prüfungen; Prüfziele Router, 1.1.1.1 und 8.8.8.8; Sim-Server als Kann-Anforderung F-10.
  - Geklärt in Phase 4: Messstart am 17.10.2026.
  - Weiter offen:
    - Ab welcher Unterbrechungsdauer trennt Le Mans Ultimate die Serververbindung? (Annahme: mehrere hundert Millisekunden bis einige Sekunden, nicht belegt.)
