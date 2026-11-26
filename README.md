# TimeTracker

## Local development with Azure SQL

The Development environment uses the Azure SQL connection in
`appsettings.Development.json` with Microsoft Entra Default authentication. Install
the Azure CLI and sign in before starting the app:

```powershell
az login
dotnet run
```

The signed-in identity must be authorized to access the database, and the SQL
server firewall must allow the development machine. Update the server and database
in `appsettings.Development.json` when using a different Azure SQL database. Do not
put passwords or other secrets in this file.

## Reporting

Select **Reporting** in the navigation to view all tracked hours by customer and
project. Open time entries are included through the current time. Use **Download
Excel** to export the same project rows and overall total as an `.xlsx` workbook.

## WPF desktop client

The `TimeTracker.Wpf` project is a Windows desktop client for the existing API at
`https://nils-timetracker-web.azurewebsites.net/`. The desktop app is not hosted
in App Service; it runs on Windows and sends authenticated requests to the
existing Azure backend. Its API client can be pointed at another HTTPS endpoint
with `TIMETRACKER_API_BASE_URL`.

The first run also requires Entra app-registration setup:

1. In the existing `TimeTracker-web` API registration, set the Application ID
   URI to `api://<api-client-id>` and expose a delegated `user_impersonation`
   scope. The WPF client requests
   `api://<api-client-id>/user_impersonation`.
2. Register a native/public client for the WPF app, add the `http://localhost`
   redirect URI, and grant it delegated permission to the API scope.
3. Set `AzureAd:ClientId` in `TimeTracker.Wpf/appsettings.json` to the native
   client registration ID. The tenant ID, API URL, and API scope can also be
   overridden with `TIMETRACKER_AZUREAD_TENANT_ID`, `TIMETRACKER_API_BASE_URL`,
   `TIMETRACKER_API_SCOPE`, and `TIMETRACKER_AZUREAD_CLIENT_ID`. No client secret
   is used by the desktop app.
4. Ensure the existing App Service Authentication configuration accepts the API
   token audience. The client uses interactive Microsoft Entra sign-in and
   sends a bearer token; it does not rely on a caller-supplied tenant header.

The WPF client supports viewing customers, projects, and time entries, plus
starting and stopping the current timer. The backend API must be redeployed to
the existing App Service before the new timer endpoints are available there.
The existing API registration currently has no delegated scope, so complete
the Entra setup before attempting sign-in. Run the desktop app on Windows with:

```powershell
dotnet run --project TimeTracker.Wpf
```

In VS Code, install the C# extension, open the repository folder, select
**TimeTracker WPF** in Run and Debug, and press **F5**. The launch configuration
builds the WPF project before starting it.
## WinForms-Client

Der WinForms-Client liegt unter `TimeTracker.WinForms`. Zum lokalen Starten
benötigt er die neben der Anwendung liegende `appsettings.json` und öffnet beim
Programmstart automatisch die interaktive Microsoft-Entra-Anmeldung.

Für eine eigenständige Windows-x64-EXE:

```powershell
dotnet publish .\TimeTracker.WinForms\TimeTracker.WinForms.csproj -c Release -r win-x64 --self-contained true
```

Die fertige Anwendung liegt anschließend unter
`TimeTracker.WinForms\bin\Release\net10.0-windows\win-x64\publish\TimeTracker.WinForms.exe`.
API-URL, Scope, Tenant-ID und Client-ID können über die von der WPF-App
verwendeten Umgebungsvariablen `TIMETRACKER_API_BASE_URL`,
`TIMETRACKER_API_SCOPE`, `TIMETRACKER_AZUREAD_TENANT_ID` und
`TIMETRACKER_AZUREAD_CLIENT_ID` überschrieben werden.

