using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ZHIMulToolkit.Core.Updates;

public sealed record GitHubReleaseAsset(string Name, Uri DownloadUri);

public sealed record GitHubRelease(
    string TagName,
    Uri ReleaseUri,
    bool IsDraft,
    bool IsPrerelease,
    IReadOnlyList<GitHubReleaseAsset> Assets);

public sealed class GitHubReleaseClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public GitHubReleaseClient(HttpClient? httpClient = null)
    {
        _ownsClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
    }

    public async Task<GitHubRelease> GetLatestStableReleaseAsync(string repository, CancellationToken cancellationToken = default)
    {
        if (!IsRepositoryName(repository))
        {
            throw new ArgumentException("Repository must use the OWNER/REPOSITORY form.", nameof(repository));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("ZHIMulToolkit/0.1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GitHubReleaseResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("GitHub returned an empty release response.");

        if (!Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var releaseUri) || releaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("The release page URL is invalid.");
        }

        var assets = release.Assets
            .Where(asset => Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .Select(asset => new GitHubReleaseAsset(asset.Name, new Uri(asset.BrowserDownloadUrl)))
            .ToArray();

        return new GitHubRelease(release.TagName, releaseUri, release.Draft, release.Prerelease, assets);
    }

    public static bool IsRepositoryName(string? repository)
    {
        if (string.IsNullOrWhiteSpace(repository)) return false;
        var parts = repository.Split('/');
        return parts.Length == 2 && parts.All(part => part.Length > 0 && part.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'));
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetResponse> Assets { get; init; } = [];
    }

    private sealed class GitHubAssetResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = string.Empty;
    }
}
