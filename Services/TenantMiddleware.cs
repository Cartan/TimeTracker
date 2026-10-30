using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TimeTracker.Data;
using TimeTracker.Models;

namespace TimeTracker.Services;

// Maps the signed-in user (from the Easy Auth header) to a row in Users and scopes the request's DbContext to that user.
// Users that are not in the Users table get 403, so tenants are created by adding rows to that table.
public class TenantMiddleware
{
    private const string LoginHeader = "X-MS-CLIENT-PRINCIPAL-NAME";
    private const string ClientPrincipalHeader = "X-MS-CLIENT-PRINCIPAL";
    private const string AadIdTokenHeader = "X-MS-TOKEN-AAD-ID-TOKEN";

    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        TimeTrackerDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var login = GetLogin(context);
        if (string.IsNullOrEmpty(login) && environment.IsDevelopment())
        {
            login = configuration["Tenancy:DevelopmentUser"]?.Trim();
        }

        var userId = string.IsNullOrEmpty(login)
            ? null
            : await db.Users
                .Where(u => u.Login == login)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(context.RequestAborted);

        if (userId is null && IsInitialUser(login, configuration))
        {
            var user = new User { Login = login! };
            db.Users.Add(user);

            try
            {
                await db.SaveChangesAsync(context.RequestAborted);
                userId = user.Id;
            }
            catch (DbUpdateException)
            {
                db.Entry(user).State = EntityState.Detached;
                userId = await db.Users
                    .Where(u => u.Login == login)
                    .Select(u => (int?)u.Id)
                    .FirstOrDefaultAsync(context.RequestAborted);

                if (userId is null)
                {
                    throw;
                }
            }
        }

        if (userId is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Your account has not been set up for this application.");
            return;
        }

        db.CurrentUserId = userId;
        await _next(context);
    }

    private static bool IsInitialUser(string? login, IConfiguration configuration) =>
        !string.IsNullOrEmpty(login) &&
        (configuration.GetSection("Tenancy:InitialUsers").Get<string[]>() ?? [])
            .Any(initialLogin => string.Equals(initialLogin?.Trim(), login, StringComparison.OrdinalIgnoreCase));

    private static string? GetLogin(HttpContext context)
    {
        var login = GetLogin(context.User);
        if (!string.IsNullOrEmpty(login))
        {
            return login;
        }

        var encodedPrincipal = context.Request.Headers[ClientPrincipalHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(encodedPrincipal))
        {
            try
            {
                var principalPayload = encodedPrincipal
                    .Replace('-', '+')
                    .Replace('_', '/');
                principalPayload = principalPayload.PadRight(
                    principalPayload.Length + (4 - principalPayload.Length % 4) % 4,
                    '=');
                var principal = JsonSerializer.Deserialize<ClientPrincipal>(
                    Encoding.UTF8.GetString(Convert.FromBase64String(principalPayload)));
                login = principal?.Claims
                    .FirstOrDefault(claim => claim.Type is
                        "preferred_username" or
                        "email" or
                        "upn" or
                        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" or
                        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?
                    .Value
                    .Trim();
                if (!string.IsNullOrEmpty(login))
                {
                    return login;
                }
            }
            catch (FormatException)
            {
                return context.Request.Headers[LoginHeader].FirstOrDefault()?.Trim();
            }
            catch (JsonException)
            {
                return context.Request.Headers[LoginHeader].FirstOrDefault()?.Trim();
            }
        }

        login = GetLoginFromIdToken(context.Request.Headers[AadIdTokenHeader].FirstOrDefault());
        if (!string.IsNullOrEmpty(login))
        {
            return login;
        }

        return context.Request.Headers[LoginHeader].FirstOrDefault()?.Trim();
    }

    private static string? GetLogin(ClaimsPrincipal principal) =>
        principal.Claims
            .FirstOrDefault(claim => claim.Type is
                "preferred_username" or
                "email" or
                "upn" or
                ClaimTypes.Email or
                ClaimTypes.Name)?
            .Value
            .Trim();

    private static string? GetLoginFromIdToken(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var parts = idToken.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var claims = JsonSerializer.Deserialize<IdTokenClaims>(
                Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return claims?.PreferredUsername?.Trim()
                ?? claims?.Email?.Trim()
                ?? claims?.Upn?.Trim();
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ClientPrincipal
    {
        [JsonPropertyName("claims")]
        public IReadOnlyList<ClientPrincipalClaim> Claims { get; init; } = [];
    }

    private sealed class ClientPrincipalClaim
    {
        [JsonPropertyName("typ")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("val")]
        public string Value { get; init; } = string.Empty;
    }

    private sealed class IdTokenClaims
    {
        [JsonPropertyName("preferred_username")]
        public string? PreferredUsername { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("upn")]
        public string? Upn { get; init; }
    }
}
