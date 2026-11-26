using TimeTracker.WinForms.Models;
using TimeTracker.WinForms.Services;
using TimeTracker.Models;
using WpfTimeEntryDto = TimeTracker.Wpf.Models.TimeEntryDto;
using Xunit;

namespace TimeTracker.Tests.Clients;

public sealed class ClientTimeRulesTests
{
    [Fact]
    public void WinFormsSettingsAcceptValidHttpsConfiguration()
    {
        var settings = new ClientSettings
        {
            Api = new ApiSettings
            {
                BaseUrl = "https://example.test/",
                Scope = "api://00000000-0000-0000-0000-000000000001/user_impersonation"
            },
            AzureAd = new AzureAdSettings
            {
                TenantId = "00000000-0000-0000-0000-000000000002",
                ClientId = "00000000-0000-0000-0000-000000000003"
            }
        };

        Assert.Null(settings.GetConfigurationIssue());
    }

    [Fact]
    public void WinFormsSettingsRejectNonHttpsApiUrls()
    {
        var settings = new ClientSettings
        {
            Api = new ApiSettings { BaseUrl = "http://example.test/", Scope = "api://scope" },
            AzureAd = new AzureAdSettings
            {
                TenantId = "00000000-0000-0000-0000-000000000002",
                ClientId = "00000000-0000-0000-0000-000000000003"
            }
        };

        Assert.Equal("Api:BaseUrl must be an absolute HTTPS URL.", settings.GetConfigurationIssue());
    }

    [Fact]
    public void WinFormsTimeEntryPreservesOffsetAndConvertsOnlyForDisplay()
    {
        var start = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.FromHours(2));
        var entry = new TimeEntryDto(1, 2, "Project", start, null, 0);

        Assert.Equal(start, entry.StartTime);
        Assert.Equal(start.ToLocalTime(), entry.StartTimeLocal);
    }

    [Fact]
    public void WpfTimeEntryPreservesOffsetAndConvertsOnlyForDisplay()
    {
        var start = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.FromHours(1));
        var entry = new WpfTimeEntryDto(1, 2, "Project", start, null, 0);

        Assert.Equal(start, entry.StartTime);
        Assert.Equal(start.ToLocalTime(), entry.StartTimeLocal);
    }

    [Fact]
    public void WebTimeEntryUsesDateTimeOffsetForPersistedStartTime()
    {
        var start = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.FromHours(1));
        var entry = new TimeEntry { StartTime = start };

        Assert.Equal(start, entry.StartTime);
    }

    [Fact]
    public void ActiveEntryElapsedTimeUsesAnOffsetIndependentUtcInstant()
    {
        var start = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.FromHours(2));
        var now = new DateTimeOffset(2026, 10, 25, 1, 45, 0, TimeSpan.FromHours(1));
        var state = new TimeTrackerState();
        state.SetEntries([new TimeEntryDto(1, 2, "Project", start, null, 0)]);

        Assert.Equal(TimeSpan.FromMinutes(75), state.GetElapsed(now));
    }
}
