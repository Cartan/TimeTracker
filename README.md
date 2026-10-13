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
