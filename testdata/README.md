# Testdaten

Es gibt zu Projektbeginn keine echten Messdaten; das ist bewusst entschieden (siehe `docs/decisions.md`).

## Automatische Tests

Die automatischen Tests erzeugen ihre Daten selbst mit einem simulierten Prüfer und einer simulierten Uhr. Pflichtfälle aus `docs/requirements.md`:

| Fall | Simuliertes Muster | Erwartung |
| --- | --- | --- |
| Kurzer Ausfall | alle Internetziele 12:00:00,000 bis 12:00:01,500 ohne Antwort, Router antwortet | ein Aussetzer, Dauer 1.500 ms ± 1 Takt, Ursache `provider` |
| Router-Neustart | Router und Internetziele ohne Antwort | Ursache `router` |
| Kabel gezogen | Adapter meldet getrennt | Ursache `pc_lan` |
| Einzelner Paketverlust | eine Prüfung an einem Ziel verloren | kein Aussetzer |
| Zeitumstellung | Aussetzer von 25.10.2026 02:59:59 MESZ bis 02:00:01 MEZ | Dauer 2 s, UTC korrekt |

## Handtests

Die Protokolle aus den Handtests kommen in diesen Ordner, benannt nach Datum und Test, zum Beispiel `2026-10-16_kabel-5s_events.csv`. Notiere zu jedem Test die Uhrzeit von Hand in `handtests.md`, damit Claude Code die erkannte Zeit vergleichen kann:

- LAN-Kabel 5 Sekunden ziehen
- Router über den Netzschalter neu starten
- Netzwerkadapter in Windows deaktivieren und wieder aktivieren
- PC in Standby schicken und aufwecken

Die Dateien enthalten nur private Router-Adressen und öffentliche DNS-Adressen; eine Anonymisierung ist nicht nötig.
