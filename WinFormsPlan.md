# Implementierungsplan: TimeTracker.WinForms

## Ziel

Eine eigenständige .NET-10-WinForms-Anwendung unter `TimeTracker.WinForms`
ergänzt die bestehende Web- und WPF-Anwendung. Sie verwendet ausschließlich
die vorhandene `api/TimeTrackerApi/...`-API und bietet dieselben Kernfunktionen:
Anmelden, Kunden und Projekte anzeigen/anlegen sowie Zeiterfassung starten,
stoppen und anzeigen.

## Umsetzungsschritte

1. **Projekt und Solution**
   - Neues `net10.0-windows`-WinExe-Projekt mit aktivierter Nullable-Analyse,
     impliziten Usings und Windows Forms anlegen.
   - Das Projekt in `TimeTracker.sln` aufnehmen.
   - Veröffentlichung analog zur WPF-App für eine selbständige x64-Single-File-
     `.exe` konfigurieren; `appsettings.json` bleibt neben der ausführbaren
     Datei und wird nicht in die Single-Datei eingebettet.

2. **Gemeinsame Client-Verträge und Konfiguration**
   - API-DTOs und Request-Typen entsprechend dem WPF-Client abbilden.
   - `appsettings.json` analog zur WPF-App mit API-Basis-URL, Scope, Tenant-ID
     und Client-ID erstellen.
   - Umgebungsvariablen für diese Werte unterstützen und Konfiguration vor dem
     Start der Hauptmaske validieren.
   - Keine Secrets oder Tokens in Quellcode bzw. Konfigurationsdateien
     aufnehmen.

3. **Authentifizierung und API**
   - Microsoft Entra Public Client mit interaktivem Login über MSAL verwenden.
   - Die Anmeldung beim Programmstart automatisch auslösen; es gibt keinen
     separaten Login-Button.
   - Access Tokens ausschließlich als Bearer-Token für API-Aufrufe verwenden.
   - HTTP-Fehler, Redirects, ungültige Antworten und Abbrüche explizit in der
     UI anzeigen, ohne erfolgreiche Fallbackwerte zu erzeugen.

4. **WinForms-Bedienoberfläche**
   - Hauptformular mit Status-/Fehleranzeige, Kunden- und Projektlisten,
     Eingaben zum Anlegen, Projektwahl sowie Start-/Stop-/Refresh-Aktionen
     erstellen.
   - Nach erfolgreicher Anmeldung Daten laden und Bedienfelder entsprechend
     des Authentifizierungs- und Busy-Zustands aktivieren/deaktivieren.
   - Laufenden Eintrag mit einem UI-Timer anzeigen; API-Aufrufe dürfen den
     UI-Thread nicht blockieren.
   - Beim Schließen HTTP-Client und Timer sauber freigeben.

5. **DateTime-/DateTimeOffset-Regeln**
   - API-Modelle verwenden `DateTimeOffset` und `DateTimeOffset?`.
   - Senden und Empfangen erfolgt offsetbehaftet; lokale Zeit wird ausschließlich
     für die Anzeige über `ToLocalTime()` berechnet.
   - Laufzeit und offene Einträge werden gegen `DateTimeOffset.UtcNow`
     berechnet, damit Sommer-/Normalzeit-Übergänge korrekt funktionieren.

6. **Tests**
   - Fokussierte Tests in `TimeTracker.Tests` für die gemeinsamen Zeitregeln
     der Web-, WPF- und WinForms-Verträge ergänzen.
   - WinForms-Basislogik testbar kapseln und Tests für Konfigurationsvalidierung,
     DTO-Zeitsemantik sowie zentrale Zustands-/Timerlogik hinzufügen.
   - Bestehende Web- und WPF-Tests unverändert weiter ausführen und die
     betroffenen Projekte kompilieren.

7. **Dokumentation und Validierung**
   - README um Startanleitung für die veröffentlichte `.exe`, die erforderliche
     `appsettings.json` und optionale Umgebungsvariablen ergänzen.
   - `dotnet test` für die fokussierten Tests, anschließend Build/Publish der
     WinForms-Anwendung ausführen.
   - Keine Commits erstellen; das übernimmt der Nutzer.

## Abnahmekriterien

- `TimeTracker.WinForms` ist Teil der Solution und lässt sich unter Windows
  bauen und als selbständige `.exe` veröffentlichen.
- Beim Start erscheint der interaktive Microsoft-Entra-Login automatisch.
- Nach erfolgreichem Login funktionieren Laden, Anlegen, Starten, Stoppen und
  Aktualisieren über die bestehende API.
- Alle Zeitwerte behalten ihren Offset und bleiben über DST-Übergänge korrekt.
- Konfigurations-, API-, Zeitsemantik- und WinForms-Basisfunktionalität ist
  durch Tests abgesichert.
- Es wurden keine Änderungen committed.
