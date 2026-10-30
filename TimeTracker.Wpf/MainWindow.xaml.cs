using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Identity.Client;
using TimeTracker.Wpf.Models;
using TimeTracker.Wpf.Services;

namespace TimeTracker.Wpf;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private ClientSettings? _settings;
    private EntraAuthenticationService? _authentication;
    private TimeTrackerApiClient? _api;
    private TimeEntryDto? _activeEntry;
    private bool _isAuthenticated;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();
        _clock.Tick += (_, _) => UpdateTimerDisplay();

        try
        {
            _settings = ClientSettings.Load();
            var issue = _settings.GetConfigurationIssue();
            if (issue is not null)
            {
                StatusText.Text = issue;
                return;
            }

            _authentication = new EntraAuthenticationService(_settings);
            _api = new TimeTrackerApiClient(
                _httpClient,
                _settings.Api.BaseUrl,
                _authentication.GetAccessTokenAsync);
            StatusText.Text = $"API: {_settings.Api.BaseUrl}";
            SignInButton.IsEnabled = true;
        }
        catch (IOException exception)
        {
            StatusText.Text = $"Could not read app settings: {exception.Message}";
        }
        catch (JsonException exception)
        {
            StatusText.Text = $"App settings contain invalid JSON: {exception.Message}";
        }
    }

    private async void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var result = await _authentication!.SignInAsync();
            _isAuthenticated = true;
            SignInButton.Content = $"Signed in: {result.Account.Username}";
            await RefreshDataAsync();
            StatusText.Text = "Connected to the Azure TimeTracker API.";
        });
    }

    private async void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            await _authentication!.SignOutAsync();
            _isAuthenticated = false;
            _activeEntry = null;
            ProjectComboBox.ItemsSource = null;
            EntriesGrid.ItemsSource = null;
            CustomersText.Text = string.Empty;
            SignInButton.Content = "Sign in";
            UpdateTimerDisplay();
            CurrentSessionText.Text = "Sign in to load your timer.";
            StatusText.Text = "Signed out.";
        });
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            await RefreshDataAsync();
            StatusText.Text = "Data refreshed.";
        });
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectComboBox.SelectedItem is not ProjectDto project)
        {
            StatusText.Text = "Select a project before starting a timer.";
            return;
        }

        await RunUiActionAsync(async () =>
        {
            await _api!.StartTimeEntryAsync(project.Id);
            await RefreshDataAsync();
            StatusText.Text = $"Started tracking {project.DisplayName}.";
        });
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeEntry is null)
        {
            StatusText.Text = "There is no active timer to stop.";
            return;
        }

        await RunUiActionAsync(async () =>
        {
            await _api!.StopTimeEntryAsync(_activeEntry.Id);
            await RefreshDataAsync();
            StatusText.Text = "Timer stopped.";
        });
    }

    private void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateControls();

    private void Window_Loaded(object sender, RoutedEventArgs e) =>
        UpdateControls();

    private void Window_Closed(object? sender, EventArgs e)
    {
        _clock.Stop();
        _httpClient.Dispose();
    }

    private async Task RefreshDataAsync()
    {
        var customersTask = _api!.GetCustomersAsync();
        var projectsTask = _api.GetProjectsAsync();
        var timeEntriesTask = _api.GetTimeEntriesAsync();
        await Task.WhenAll(customersTask, projectsTask, timeEntriesTask);

        var selectedProjectId = (ProjectComboBox.SelectedItem as ProjectDto)?.Id;
        var projects = await projectsTask;
        var entries = await timeEntriesTask;

        ProjectComboBox.ItemsSource = projects;
        ProjectComboBox.SelectedItem = projects.FirstOrDefault(project => project.Id == selectedProjectId)
            ?? projects.FirstOrDefault();
        EntriesGrid.ItemsSource = entries;
        CustomersText.Text = $"{(await customersTask).Count} customers · {projects.Count} projects";

        _activeEntry = entries
            .Where(entry => entry.EndTime is null)
            .OrderByDescending(entry => entry.StartTime)
            .FirstOrDefault();
        UpdateTimerDisplay();
        UpdateControls();
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        catch (ApiRequestException exception)
        {
            StatusText.Text = exception.Message;
        }
        catch (MsalException exception)
        {
            StatusText.Text = $"Microsoft Entra sign-in failed: {exception.Message}";
        }
        catch (HttpRequestException exception)
        {
            StatusText.Text = $"Could not reach the Azure API: {exception.Message}";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = "The request timed out. Check the connection and try again.";
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = exception.Message;
        }
        catch (JsonException exception)
        {
            StatusText.Text = $"The API returned invalid JSON: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UpdateControls();
        if (isBusy)
        {
            StatusText.Text = "Working...";
        }
    }

    private void UpdateControls()
    {
        SignInButton.IsEnabled = !_isBusy && _settings is not null && _authentication is not null;
        SignOutButton.IsEnabled = !_isBusy && _isAuthenticated && _authentication is not null;
        RefreshButton.IsEnabled = !_isBusy && _isAuthenticated;
        ProjectComboBox.IsEnabled = !_isBusy && _isAuthenticated;
        StartButton.IsEnabled =
            !_isBusy &&
            _isAuthenticated &&
            ProjectComboBox.SelectedItem is ProjectDto selectedProject &&
            _activeEntry?.ProjectId != selectedProject.Id;
        StopButton.IsEnabled = !_isBusy && _isAuthenticated && _activeEntry is not null;
    }

    private void UpdateTimerDisplay()
    {
        if (_activeEntry is null)
        {
            CurrentSessionText.Text = "No timer running";
            ElapsedText.Text = "--:--:--";
            _clock.Stop();
            return;
        }

        CurrentSessionText.Text = $"{_activeEntry.ProjectName}\nStarted {_activeEntry.StartTime.ToLocalTime():HH:mm}";
        var elapsed = DateTime.UtcNow - _activeEntry.StartTime.ToUniversalTime();
        ElapsedText.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        _clock.Start();
    }
}
