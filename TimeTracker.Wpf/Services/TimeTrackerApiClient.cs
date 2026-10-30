using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TimeTracker.Wpf.Models;

namespace TimeTracker.Wpf.Services;

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

    public Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<ProjectDto>>("api/TimeTrackerApi/projects", cancellationToken);

    public Task<IReadOnlyList<TimeEntryDto>> GetTimeEntriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<TimeEntryDto>>("api/TimeTrackerApi/timeentries", cancellationToken);

    public Task<TimeEntryDto> StartTimeEntryAsync(int projectId, CancellationToken cancellationToken = default) =>
        PostAsync<TimeEntryDto, StartTimeEntryRequest>(
            "api/TimeTrackerApi/timeentries/start",
            new StartTimeEntryRequest(projectId),
            cancellationToken);

    public Task<TimeEntryDto> StopTimeEntryAsync(int entryId, CancellationToken cancellationToken = default) =>
        PostAsync<TimeEntryDto, object>(
            $"api/TimeTrackerApi/timeentries/{entryId}/stop",
            new { },
            cancellationToken);

    private async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseAddress, path));
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TResponse, TRequest>(
        string path,
        TRequest content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseAddress, path))
        {
            Content = JsonContent.Create(content, options: JsonOptions)
        };
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await _getAccessToken(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ApiRequestException(response.StatusCode, detail);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The TimeTracker API returned an empty response.");
    }
}

public sealed class ApiRequestException : Exception
{
    public ApiRequestException(HttpStatusCode statusCode, string responseBody)
        : base(CreateMessage(statusCode, responseBody))
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }

    private static string CreateMessage(HttpStatusCode statusCode, string responseBody)
    {
        if ((int)statusCode is >= 300 and < 400)
        {
            return "The App Service redirected the API request to sign-in. Check the configured delegated API scope and token audience.";
        }

        var message = $"The TimeTracker API returned HTTP {(int)statusCode} ({statusCode}).";
        var detail = responseBody.Trim();
        return detail.Length == 0 ? message : $"{message} {detail[..Math.Min(detail.Length, 300)]}";
    }
}
