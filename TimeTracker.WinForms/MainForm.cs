using System.Net.Http;
using System.Text.Json;
using Microsoft.Identity.Client;
using TimeTracker.WinForms.Models;
using TimeTracker.WinForms.Services;

namespace TimeTracker.WinForms;

public sealed class MainForm : Form
{
    private readonly HttpClient _httpClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(90)
    };
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    private readonly Label _statusLabel = new() { AutoSize = true };
    private readonly Label _sessionLabel = new() { AutoSize = true };
    private readonly Label _elapsedLabel = new() { AutoSize = true, Text = "--:--:--" };
    private readonly ComboBox _customerComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _projectComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _customerNameTextBox = new();
    private readonly TextBox _projectNameTextBox = new();
    private readonly Button _addCustomerButton = new() { Text = "Add customer" };
    private readonly Button _addProjectButton = new() { Text = "Add project" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _stopButton = new() { Text = "Stop" };
    private readonly Button _refreshButton = new() { Text = "Refresh" };
    private readonly DataGridView _entriesGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };

    private ClientSettings? _settings;
    private EntraAuthenticationService? _authentication;
    private TimeTrackerApiClient? _api;
    private readonly TimeTrackerState _state = new();
    private bool _isAuthenticated;
    private bool _isBusy;

    public MainForm()
    {
        Text = "TimeTracker";
        Width = 1000;
        Height = 700;
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        _clock.Tick += (_, _) => UpdateTimerDisplay();
        _customerComboBox.SelectedIndexChanged += (_, _) => UpdateControls();
        _projectComboBox.SelectedIndexChanged += (_, _) => UpdateControls();
        _customerNameTextBox.TextChanged += (_, _) => UpdateControls();
        _projectNameTextBox.TextChanged += (_, _) => UpdateControls();
        Shown += MainForm_Shown;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var statusPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        statusPanel.Controls.Add(_statusLabel);
        statusPanel.Controls.Add(_refreshButton);
        _refreshButton.Click += async (_, _) => await RunUiActionAsync(RefreshDataAsync, "Data refreshed.");

        var timerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        timerPanel.Controls.Add(new Label { Text = "Session:", AutoSize = true });
        timerPanel.Controls.Add(_sessionLabel);
        timerPanel.Controls.Add(new Label { Text = "Elapsed:", AutoSize = true, Margin = new Padding(20, 3, 0, 0) });
        timerPanel.Controls.Add(_elapsedLabel);
        timerPanel.Controls.Add(_startButton);
        timerPanel.Controls.Add(_stopButton);
        _startButton.Click += async (_, _) => await StartAsync();
        _stopButton.Click += async (_, _) => await StopAsync();

        var createPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        createPanel.Controls.Add(new Label { Text = "Customer:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) });
        _customerNameTextBox.Width = 150;
        createPanel.Controls.Add(_customerNameTextBox);
        createPanel.Controls.Add(_addCustomerButton);
        createPanel.Controls.Add(new Label { Text = "Project:", AutoSize = true, Margin = new Padding(20, 5, 0, 0) });
        _projectNameTextBox.Width = 150;
        createPanel.Controls.Add(_projectNameTextBox);
        createPanel.Controls.Add(_addProjectButton);
        _addCustomerButton.Click += async (_, _) => await AddCustomerAsync();
        _addProjectButton.Click += async (_, _) => await AddProjectAsync();

        var selectionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        selectionPanel.Controls.Add(new Label { Text = "Customer:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) });
        _customerComboBox.Width = 220;
        selectionPanel.Controls.Add(_customerComboBox);
        selectionPanel.Controls.Add(new Label { Text = "Project:", AutoSize = true, Margin = new Padding(20, 5, 0, 0) });
        _projectComboBox.Width = 300;
        selectionPanel.Controls.Add(_projectComboBox);

        root.Controls.Add(statusPanel, 0, 0);
        root.Controls.Add(timerPanel, 0, 1);
        root.Controls.Add(createPanel, 0, 2);
        root.Controls.Add(selectionPanel, 0, 3);
        root.Controls.Add(_entriesGrid, 0, 4);
        Controls.Add(root);
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        try
        {
            _settings = ClientSettings.Load();
            var issue = _settings.GetConfigurationIssue();
            if (issue is not null)
            {
                ShowError(issue);
                return;
            }

            _authentication = new EntraAuthenticationService(_settings);
            _api = new TimeTrackerApiClient(_httpClient, _settings.Api.BaseUrl, _authentication.GetAccessTokenAsync);
            _statusLabel.Text = "Signing in...";
            SetBusy(true);
            await _authentication.SignInAsync();
            _isAuthenticated = true;
            await RefreshDataAsync();
            _statusLabel.Text = "Connected to the TimeTracker API.";
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or MsalException or
            HttpRequestException or TaskCanceledException or InvalidDataException or ApiRequestException)
        {
            ShowError(GetExceptionMessage(exception));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task StartAsync()
    {
        if (_projectComboBox.SelectedItem is not ProjectDto project)
        {
            ShowError("Select a project before starting a timer.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            await _api!.StartTimeEntryAsync(project.Id);
            await RefreshDataAsync();
        }, $"Started tracking {project.DisplayName}.");
    }

    private async Task StopAsync()
    {
        if (_state.ActiveEntry is null)
        {
            ShowError("There is no active timer to stop.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            await _api!.StopTimeEntryAsync(_state.ActiveEntry.Id);
            await RefreshDataAsync();
        }, "Timer stopped.");
    }

    private async Task AddCustomerAsync()
    {
        var name = _customerNameTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("Enter a customer name.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            await _api!.CreateCustomerAsync(name);
            _customerNameTextBox.Clear();
            await RefreshDataAsync();
        }, $"Added customer {name}.");
    }

    private async Task AddProjectAsync()
    {
        var name = _projectNameTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("Enter a project name.");
            return;
        }

        if (_customerComboBox.SelectedItem is not CustomerDto customer)
        {
            ShowError("Select a customer before adding a project.");
            return;
        }

        await RunUiActionAsync(async () =>
        {
            await _api!.CreateProjectAsync(name, customer.Id);
            _projectNameTextBox.Clear();
            await RefreshDataAsync();
        }, $"Added project {name}.");
    }

    private async Task RefreshDataAsync()
    {
        var customersTask = _api!.GetCustomersAsync();
        var projectsTask = _api.GetProjectsAsync();
        var entriesTask = _api.GetTimeEntriesAsync();
        await Task.WhenAll(customersTask, projectsTask, entriesTask);

        var customers = await customersTask;
        var projects = await projectsTask;
        var entries = await entriesTask;
        _customerComboBox.DataSource = customers.ToList();
        _customerComboBox.DisplayMember = nameof(CustomerDto.Name);
        _projectComboBox.DataSource = projects.ToList();
        _projectComboBox.DisplayMember = nameof(ProjectDto.DisplayName);
        _entriesGrid.DataSource = entries.Select(entry => new
        {
            entry.Id,
            entry.ProjectName,
            Start = entry.StartTimeLocal,
            End = entry.EndTimeLocal,
            entry.DurationMinutes
        }).ToList();
        _state.SetEntries(entries);
        UpdateTimerDisplay();
        UpdateControls();
    }

    private async Task RunUiActionAsync(Func<Task> action, string successMessage)
    {
        SetBusy(true);
        try
        {
            await action();
            _statusLabel.Text = successMessage;
        }
        catch (Exception exception) when (exception is ApiRequestException or HttpRequestException or TaskCanceledException or InvalidDataException or JsonException or MsalException)
        {
            ShowError(GetExceptionMessage(exception));
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
            _statusLabel.Text = "Working...";
        }
    }

    private void UpdateControls()
    {
        var enabled = _isAuthenticated && !_isBusy;
        _refreshButton.Enabled = enabled;
        _customerComboBox.Enabled = enabled;
        _projectComboBox.Enabled = enabled;
        _customerNameTextBox.Enabled = enabled;
        _projectNameTextBox.Enabled = enabled;
        _addCustomerButton.Enabled = enabled && !string.IsNullOrWhiteSpace(_customerNameTextBox.Text);
        _addProjectButton.Enabled = enabled &&
            !string.IsNullOrWhiteSpace(_projectNameTextBox.Text) &&
            _customerComboBox.SelectedItem is CustomerDto;
        _startButton.Enabled = enabled &&
            _projectComboBox.SelectedItem is ProjectDto project &&
            _state.ActiveEntry?.ProjectId != project.Id;
        _stopButton.Enabled = enabled && _state.ActiveEntry is not null;
    }

    private void UpdateTimerDisplay()
    {
        if (_state.ActiveEntry is null)
        {
            _sessionLabel.Text = "No timer running";
            _elapsedLabel.Text = "--:--:--";
            _clock.Stop();
            return;
        }

        _sessionLabel.Text = $"{_state.ActiveEntry.ProjectName} (started {_state.ActiveEntry.StartTimeLocal:HH:mm})";
        var elapsed = _state.GetElapsed(DateTimeOffset.UtcNow);
        _elapsedLabel.Text = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        _clock.Start();
    }

    private void ShowError(string message) => _statusLabel.Text = message;

    private static string GetExceptionMessage(Exception exception) => exception switch
    {
        ApiRequestException or HttpRequestException or InvalidDataException => exception.Message,
        TaskCanceledException => "The request timed out. Check the connection and try again.",
        MsalException => $"Microsoft Entra sign-in failed: {exception.Message}",
        JsonException => $"The API returned invalid JSON: {exception.Message}",
        _ => exception.Message
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
            _httpClient.Dispose();
        }

        base.Dispose(disposing);
    }
}
