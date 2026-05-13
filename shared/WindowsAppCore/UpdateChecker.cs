using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WindowsAppCore;

public sealed class UpdateChecker
{
    private readonly HttpClient _httpClient;
    private readonly Version? _currentVersion;
    private readonly string _endpoint;

    public UpdateChecker(HttpClient httpClient, string currentVersion, string repoOwner, string repoName)
    {
        _httpClient = httpClient;
        // If the running version isn't parseable (local dev without stamping), skip checks entirely.
        _currentVersion = Version.TryParse(currentVersion, out var v) ? v : null;
        _endpoint = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{repoName}/updater");
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        if (_currentVersion is null)
            return UpdateCheckResult.NoUpdate;

        try
        {
            var response = await _httpClient.GetAsync(_endpoint, ct);
            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.NoUpdate;

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(ct);
            if (release?.TagName is not { Length: > 0 } rawTag)
                return UpdateCheckResult.NoUpdate;

            var versionStr = rawTag.TrimStart('v');
            if (!Version.TryParse(versionStr, out var latest))
                return UpdateCheckResult.NoUpdate;

            return latest > _currentVersion
                ? new UpdateCheckResult(true, versionStr, release.HtmlUrl)
                : UpdateCheckResult.NoUpdate;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return UpdateCheckResult.NoUpdate;
        }
    }

    public void StartPeriodicChecks(TimeSpan interval, Action<UpdateCheckResult> onUpdateAvailable, CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await CheckAsync(ct);
                    if (result.IsUpdateAvailable)
                        onUpdateAvailable(result);
                }
                catch (OperationCanceledException) { break; }
                catch { /* non-fatal */ }

                try { await Task.Delay(interval, ct); }
                catch (OperationCanceledException) { break; }
            }
        }, CancellationToken.None);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}
