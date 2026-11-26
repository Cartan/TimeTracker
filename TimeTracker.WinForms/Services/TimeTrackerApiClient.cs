using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TimeTracker.WinForms.Models;

namespace TimeTracker.WinForms.Services;

public sealed class TimeTrackerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly Uri _baseAddress;
    private readonly Func<CancellationToken, Task<string>> _getAccessToken;

    public TimeTrackerApiClient(
        HttpClient httpClient,
        string baseUrl,
        Func<CancellationToken, Task<string>> getAccessToken)
    {
        _httpClient = httpClient;
        _baseAddress = new Uri(baseUrl, UriKind.Absolute);
        _getAccessToken = getAccessToken;
    }

    public Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<CustomerDto>>("api/TimeTrackerApi/customers", cancellationToken);

    public Task<CustomerDto> CreateCustomerAsync(string name, CancellationToken cancellationToken = default) =>
        PostAsync<CustomerDto, CreateCustomerRequest>(
            "api/TimeTrackerApi/customers", new(name), cancellationToken);

    public Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<ProjectDto>>("api/TimeTrackerApi/projects", cancellationToken);

    public Task<ProjectDto> CreateProjectAsync(string name, int customerId, CancellationToken cancellationToken = default) =>
        PostAsync<ProjectDto, CreateProjectRequest>(
            "api/TimeTrackerApi/projects", new(name, customerId), cancellationToken);

    public Task<IReadOnlyList<TimeEntryDto>> GetTimeEntriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<TimeEntryDto>>("api/TimeTrackerApi/timeentries", cancellationToken);

    public Task<TimeEntryDto> StartTimeEntryAsync(int projectId, CancellationToken cancellationToken = default) =>
        PostAsync<TimeEntryDto, StartTimeEntryRequest>(
            "api/TimeTrackerApi/timeentries/start", new(projectId), cancellationToken);

    public Task<TimeEntryDto> StopTimeEntryAsync(int entryId, CancellationToken cancellationToken = default) =>
        PostAsync<TimeEntryDto, object>($"api/TimeTrackerApi/timeentries/{entryId}/stop", new { }, cancellationToken);

    private Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken) =>
        SendAsync<TResponse>(new HttpRequestMessage(HttpMethod.Get, new Uri(_baseAddress, path)), cancellationToken);

    private Task<TResponse> PostAsync<TResponse, TRequest>(
        string path, TRequest content, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseAddress, path))
        {
            Content = JsonContent.Create(content, options: JsonOptions)
        };
        return SendAsync<TResponse>(request, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            var accessToken = await _getAccessToken(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new ApiRequestException(response.StatusCode, detail);
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The TimeTracker API returned an empty response.");
        }
    }
}

public sealed class ApiRequestException(HttpStatusCode statusCode, string responseBody)
    : Exception(CreateMessage(statusCode, responseBody))
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    private static string CreateMessage(HttpStatusCode statusCode, string responseBody)
    {
        if ((int)statusCode is >= 300 and < 400)
        {
            return "The API redirected the request to sign-in. Check the configured delegated API scope and token audience.";
        }

        var message = $"The TimeTracker API returned HTTP {(int)statusCode} ({statusCode}).";
        var detail = responseBody.Trim();
        return detail.Length == 0 ? message : $"{message} {detail[..Math.Min(detail.Length, 300)]}";
    }
}
