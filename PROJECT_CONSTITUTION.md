# TimeTracker Project Constitution

## 1. Zweck und Geltungsbereich

Diese Constitution ist die verbindliche technische Leitlinie für Änderungen an
TimeTracker. Sie ist aus der bestehenden Codebasis abgeleitet und ergänzt die
konkreten Ausführungsanweisungen in `.github/copilot-instructions.md`.

TimeTracker besteht aus:

- einer ASP.NET-Core-MVC-Webanwendung im Root-Projekt,
- einer separaten Windows-WPF-Anwendung in `TimeTracker.Wpf`,
- einer fokussierten xUnit-Testsuite in `TimeTracker.Tests`,
- einer gemeinsamen fachlichen API für Kunden, Projekte und Zeiterfassung.

Bei Zielkonflikten haben Datenisolation, Authentifizierung und Erhalt der
bestehenden Client-Verträge Vorrang vor Bequemlichkeit oder einer verkürzten
Implementierung.

## 2. Architekturprinzipien

1. **Bestehende Schichten respektieren.** Web-Code bleibt in den etablierten
   Bereichen `Controllers`, `Data`, `Dtos`, `Models`, `Services`,
   `ViewModels` und `Views`. Neue Verantwortung wird der fachlich passenden
   Schicht zugeordnet, statt Controller oder Views unkontrolliert zu vergrößern.
2. **MVC und API getrennt halten.** HTML-Interaktionen verwenden MVC-Controller,
   Razor-Views, ViewModels und Anti-Forgery-Schutz. Der WPF-Client verwendet
   ausschließlich die stabilen API-Routen unter `api/`.
3. **Dependency Injection und EF Core verwenden.** Datenbankzugriffe laufen
   über `TimeTrackerDbContext` und die ASP.NET-Core-DI. Neue Persistenzlogik
   darf keine parallelen, unverwalteten Datenzugriffspfade einführen.
4. **Asynchronität durchgängig beibehalten.** I/O-Operationen verwenden die
   asynchronen EF-Core- und HTTP-APIs und reichen `CancellationToken` weiter,
   wenn der Aufrufer ihn bereits besitzt.
5. **Nullable Reference Types ernst nehmen.** Die Projekte verwenden aktivierte
   Nullable-Analyse und implizite Usings. Nullzustände werden durch Typen,
   Guards und Validierung modelliert; unnötige Casts und Null-forgiving
   Operatoren sind zu vermeiden.

## 3. Mandantenfähigkeit und Sicherheit

Tenant-Isolation ist eine nicht verhandelbare Invariante.

- Die Identität wird aus dem validierten Bearer-Token oder den von App Service
  bereitgestellten Client-Principal-/ID-Token-Informationen abgeleitet.
- Ein vom Aufrufer gesetzter Tenant-Header ist keine vertrauenswürdige
  Identitätsquelle und darf nicht eingeführt oder zur Autorisierung verwendet
  werden.
- `TenantMiddleware` löst den Login auf einen `User`-Datensatz auf,
  verweigert unbekannte Konten mit HTTP 403 und setzt anschließend
  `TimeTrackerDbContext.CurrentUserId`.
- `Customer`, `Project` und `TimeEntry` besitzen EF-Core-Query-Filter auf den
  aktuellen Benutzer. Neue tenantbezogene Entitäten müssen demselben Muster
  folgen.
- Beim Speichern weist der DbContext neu angelegten tenantbezogenen Entitäten
  automatisch den aktuellen Benutzer zu. Direkte UserId-Eingaben aus Requests
  dürfen diese Regel nicht umgehen.
- `CurrentUserId == null` ist ausschließlich für kontrollierte Startup-,
  Migrations- oder Test-Szenarien zulässig. Produktionsrequests müssen durch
  die Middleware einen bekannten Tenant erhalten.
- Änderungen an Authentifizierung, Claims-Auflösung oder Middleware benötigen
  fokussierte Tests für erlaubte, unbekannte und fehlende Identitäten.
- Secrets, Passwörter, Access Tokens und passwortbehaftete Connection Strings
  werden niemals committed. Entwicklung nutzt Azure SQL mit Microsoft Entra
  Default Authentication; die WPF-Anwendung verwendet interaktive Anmeldung
  ohne Client Secret.

## 4. Datenmodell und Zeitsemantik

- Die fachliche Hierarchie lautet `User -> Customer -> Project -> TimeEntry`.
- Beziehungen und Löschverhalten werden zentral im `TimeTrackerDbContext`
  definiert. Tenant-Grenzen dürfen nicht durch Navigationen, Includes oder
  alternative Queries umgangen werden.
- Zeitstempel von Zeiteinträgen sind `DateTimeOffset` und werden in SQL Server
  als `datetimeoffset` gespeichert. Dadurch bleiben Offsets erhalten.
- Anwendungscode erzeugt neue Zeitstempel mit `DateTimeOffset.UtcNow`.
  Lokale Zeit wird nur für die Darstellung im Client berechnet.
- Offene Zeiteinträge (`EndTime == null`) laufen bis zum aktuellen Zeitpunkt
  weiter. Starten eines anderen Projekts beendet den vorherigen aktiven
  Eintrag desselben Tenants.
- Neue Datenbankänderungen müssen über den bestehenden EF-Core-/Schema-
  Upgrade-Mechanismus kompatibel und wiederholbar umgesetzt werden. Bestehende
  Daten dürfen nicht stillschweigend verworfen werden.
- Eingaben werden an der API-Grenze validiert und vor dem Speichern
  normalisiert, insbesondere Namen durch Trimmen. Fachliche Fremdschlüssel
  werden tenantgefiltert aufgelöst.

## 5. API- und Client-Verträge

Die API ist ein Vertrag zwischen Backend und WPF-Client.

- Vorhandene Routen, HTTP-Methoden, Request-/Response-Formen und
  `DateTimeOffset`-Semantik werden rückwärtskompatibel behandelt.
- Änderungen an DTOs oder Endpunkten müssen unmittelbar im WPF-Client
  nachvollzogen und durch API- sowie Client-kompilierung abgesichert werden.
- Ein Zugriff auf einen nicht vorhandenen oder fremden tenantbezogenen
  Datensatz wird nicht als erfolgreicher Zugriff maskiert; die bestehende API
  verwendet dafür insbesondere `404 NotFound`.
- Fehler werden explizit signalisiert. Der WPF-Client darf leere oder
  success-förmige Fallbacks nicht anstelle eines fehlgeschlagenen Requests
  erzeugen.
- Neue Endpunkte übernehmen die bestehende Benennung unter
  `api/TimeTrackerApi/...`, verwenden typisierte DTOs und akzeptieren keine
  impliziten Identitäts- oder Tenantparameter aus dem Request.

## 6. Web- und Desktop-UI

- MVC-POST-Aktionen verwenden `ValidateAntiForgeryToken`, validieren
  `ModelState` und geben bei ungültigen Eingaben das Formular mit den nötigen
  Auswahlwerten zurück.
- Controller sortieren Listen deterministisch und projizieren für API- oder
  Reporting-Ausgaben nur die benötigten Felder.
- Reporting muss offene Einträge bis zum Zeitpunkt der Berechnung einbeziehen
  und dieselben fachlichen Werte für HTML- und Excel-Ausgabe verwenden.
- Die WPF-Anwendung bleibt ein Windows-Client. Konfiguration für API-URL,
  Tenant, Scope und Client-ID kann über Umgebungsvariablen überschrieben
  werden; sensible Geheimnisse gehören nicht in `appsettings.json`.
- WPF zeigt Zeitstempel lokal an, sendet und empfängt sie jedoch als
  offsetbehaftete Werte.

## 7. Qualitätssicherung

Jede Verhaltensänderung erhält fokussierte Tests in `TimeTracker.Tests`.
Mindestens folgende Eigenschaften müssen bei betroffenen Änderungen geschützt
werden:

- Tenant-Auflösung und Zugriffsschutz,
- Query-Filter und automatische Tenant-Zuweisung,
- Request-Validierung und fremde Fremdschlüssel,
- Start-/Stop-Semantik und Dauerberechnung,
- API-DTO-Kompatibilität,
- Zeitstempeltyp und SQL-Spaltentyp.

Vor der Übergabe:

1. den kleinsten passenden Testlauf mit `dotnet test` ausführen,
2. bei Änderungen an mehreren Projekten zusätzlich die betroffenen Projekte
   bauen,
3. bestehende Nullable-, Build- und Formatierungsfehler nicht durch
   Warnungsunterdrückung verdecken,
4. keine generierten Artefakte aus `bin` oder `obj` als Quelländerung
   aufnehmen.

## 8. Änderungsregeln

- Änderungen sind klein, nachvollziehbar und auf die Anforderung begrenzt.
- Vor neuer Logik ist nach vorhandenen Helfern, Mustern und Tests zu suchen.
- Öffentliche API-, Datenmodell- oder Authentifizierungsänderungen werden in
  `README.md` und den betroffenen Tests dokumentiert.
- Ein bewusst geändertes Verhalten muss im Code, in Tests und – falls
  nutzerrelevant – in der Dokumentation sichtbar sein.
- Sicherheits- oder Datenintegritätsbedenken werden nicht durch Defaults,
  breite Exceptions, stilles Ignorieren oder untypisierte Casts kaschiert.
- Diese Constitution wird gemeinsam mit der Codeänderung aktualisiert, wenn
  sich eine der oben beschriebenen grundlegenden Invarianten ändert.

## 9. Entscheidungsmaßstab

Bei neuen Features gilt in dieser Reihenfolge:

1. Daten bleiben dem richtigen Tenant zugeordnet.
2. Authentifizierung und Autorisierung bleiben explizit und überprüfbar.
3. Backend und WPF-Client bleiben vertragskompatibel.
4. Zeit- und Reportingwerte bleiben korrekt und reproduzierbar.
5. Die Änderung passt in die bestehenden Schichten und ist fokussiert testbar.

