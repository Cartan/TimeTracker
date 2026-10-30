using Microsoft.Identity.Client;

namespace TimeTracker.Wpf.Services;

public sealed class EntraAuthenticationService
{
    private readonly IPublicClientApplication _application;
    private readonly string[] _scopes;
    private IAccount? _account;

    public EntraAuthenticationService(ClientSettings settings)
    {
        _scopes = [settings.Api.Scope];
        _application = PublicClientApplicationBuilder
            .Create(settings.AzureAd.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, settings.AzureAd.TenantId)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    public async Task<AuthenticationResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        var result = await _application
            .AcquireTokenInteractive(_scopes)
            .WithPrompt(Prompt.SelectAccount)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync(cancellationToken);

        _account = result.Account;
        return result;
    }

    public async Task SignOutAsync()
    {
        foreach (var account in await _application.GetAccountsAsync())
        {
            await _application.RemoveAsync(account);
        }

        _account = null;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        _account ??= (await _application.GetAccountsAsync()).FirstOrDefault();
        if (_account is null)
        {
            return (await SignInAsync(cancellationToken)).AccessToken;
        }

        try
        {
            var result = await _application
                .AcquireTokenSilent(_scopes, _account)
                .ExecuteAsync(cancellationToken);
            _account = result.Account;
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            return (await SignInAsync(cancellationToken)).AccessToken;
        }
    }
}
