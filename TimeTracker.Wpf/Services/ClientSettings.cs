using System.IO;
using System.Text.Json;

namespace TimeTracker.Wpf.Services;

public sealed record ClientSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiSettings Api { get; init; } = new();

    public AzureAdSettings AzureAd { get; init; } = new();

    public static ClientSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var settings = JsonSerializer.Deserialize<ClientSettings>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("The WPF app settings file is empty.");

        return settings with
        {
            Api = settings.Api with
            {
                BaseUrl = Environment.GetEnvironmentVariable("TIMETRACKER_API_BASE_URL") ?? settings.Api.BaseUrl,
                Scope = Environment.GetEnvironmentVariable("TIMETRACKER_API_SCOPE") ?? settings.Api.Scope
            },
            AzureAd = settings.AzureAd with
            {
                TenantId = Environment.GetEnvironmentVariable("TIMETRACKER_AZUREAD_TENANT_ID") ?? settings.AzureAd.TenantId,
                ClientId = Environment.GetEnvironmentVariable("TIMETRACKER_AZUREAD_CLIENT_ID") ?? settings.AzureAd.ClientId
            }
        };
    }

    public string? GetConfigurationIssue()
    {
        if (!Uri.TryCreate(Api.BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return "Api:BaseUrl must be an absolute HTTPS URL.";
        }

        if (!Guid.TryParse(AzureAd.TenantId, out _))
        {
            return "Set AzureAd:TenantId to the Microsoft Entra tenant ID.";
        }

        if (!Guid.TryParse(AzureAd.ClientId, out _))
        {
            return "Set AzureAd:ClientId to the WPF public-client app registration ID.";
        }

        if (string.IsNullOrWhiteSpace(Api.Scope) || !Api.Scope.StartsWith("api://", StringComparison.Ordinal))
        {
            return "Set Api:Scope to the delegated scope exposed by the TimeTracker API registration.";
        }

        return null;
    }
}

public sealed record ApiSettings
{
    public string BaseUrl { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;
}

public sealed record AzureAdSettings
{
    public string TenantId { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;
}
