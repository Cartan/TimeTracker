# TimeTracker contributor guidance

- The root `TimeTracker.csproj` is a .NET 10 ASP.NET Core MVC application. Keep web application code in the established `Controllers`, `Data`, `Dtos`, `Models`, `Services`, `ViewModels`, and `Views` folders.
- `TimeTracker.Wpf` is a separate Windows WPF client. Keep shared API contracts compatible with its existing client code when changing backend endpoints.
- Add or update focused tests in `TimeTracker.Tests` for behavioral changes. Run the smallest relevant `dotnet test` command after changes.
- Preserve nullable reference types and implicit usings; avoid unnecessary casts or null-forgiving operators.
- Do not add credentials, connection strings containing passwords, access tokens, or other secrets to source-controlled configuration. Development uses Azure SQL with Microsoft Entra Default authentication.
- Follow existing ASP.NET Core dependency-injection and Entity Framework Core patterns. Keep tenant isolation intact when accessing or mutating persisted data.
