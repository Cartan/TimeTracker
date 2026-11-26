m# Projekt-Spezifikation für den  WinForms Client
## Beschreibung

Zusätzlich zur Web App und der WPF App soll noch eine auf .NET WinForms basierte App erstellt werden. Wie die WPF App benutzt sie das Backend der Web App und bietet dieselbe Funktionalität wie diese beiden.

# Regeln

- Beachte bei der Implementierung die PROJECT_CONSTITUTION.md
- Erstelle das Projekt unter ./TimeTracker.WinForms
- Benutze das Backend von der Web-App
- Erstelle config Files analog zur WPF App, so daß die App
lokal gestartet werden kann.
- Der User muß sich beim Starten der App einloggen, nicht mit einem extra Button

- Beachte die Regeln für DateTime und DateTimeOffset
- Die App muß auch  nach einem Übergang zu Sommerzeit und Normalzeit noch funktionieren.
- Erstelle Tests für alle drei Apps, die sicherstellen, daß in allen Apps die DateTimeregeln eingehalten werden.
- Erstelle auch Tests für die Basisfunktionalität der WinForms App.

- Schreibe vor Beginn der Implementierung deinen Plan in eine Datei WinFormsPlan.md und beginne erst, wenn ich den Plan akzeptiert habe.

- git commit mache ich selbst, nicht du.

- Erkläre, wie ich die App starten kann. Lieber durch eine .exe als durch ´dotnet run´. 
