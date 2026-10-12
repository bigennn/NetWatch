# Erfundene Beispielwoche

**Alle Daten in diesem Ordner sind erfunden.** Sie dienen nur dazu, den Wochenbericht (M5) mit einem typischen Muster anzusehen und zu prüfen. Erzeugt wurden sie mit den Formatierern aus `NetWatch.Core`, das Format entspricht also genau den echten Protokollen.

Inhalt (06.10.–12.10.2026, Ortszeit):

- täglicher Router-Neustart gegen 02:00 (ca. 2 min 20 s), am 12.10. nicht gemessen, weil der PC im Standby war
- Messlücke am 09.10. von 10:07:30 bis 14:00 (PC aus), Standby vom 11.10. 23:30 bis 12.10. 07:00
- ein kurzer `pc_lan`-Aussetzer, zwei kurze Providerausfälle um 21:05 an zwei Tagen, ein Providerausfall von gut 4 Minuten, einer von 6,3 s

Bericht erzeugen:

```
NetWatch.exe --report-week testdata\beispiel_woche_erfunden --bis 2026-10-12
```

Ergebnis: `report_week.html` in diesem Ordner.
