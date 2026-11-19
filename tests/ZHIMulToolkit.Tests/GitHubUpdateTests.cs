using System.Net;
using System.Text;
using Xunit;
using ZHIMulToolkit.Core.Updates;

namespace ZHIMulToolkit.Tests;

public sealed class GitHubUpdateTests
{
    [Fact]
    public async Task UpdateCheckerReturnsStableNewerReleaseAndAssets()
    {
        const string json = """
            {
              "tag_name": "v0.2.0",
              "html_url": "https://github.com/example/ZHIMulToolkit/releases/tag/v0.2.0",
              "draft": false,
              "prerelease": false,
              "assets": [
                { "name": "ZHIMulToolkit.exe", "browser_download_url": "https://github.com/example/ZHIMulToolkit/releases/download/v0.2.0/ZHIMulToolkit.exe" },
                { "name": "SHA256SUMS.txt", "browser_download_url": "https://github.com/example/ZHIMulToolkit/releases/download/v0.2.0/SHA256SUMS.txt" }
              ]
            }
            """;
        using var client = CreateClient(json);
        var checker = new UpdateChecker(client);

        var update = await checker.CheckAsync("example/ZHIMulToolkit", "0.1.0");

        Assert.NotNull(update);
        Assert.Equal("0.2.0", update.Version.ToString());
        Assert.Equal("v0.2.0", update.TagName);
        Assert.Equal("https://github.com/example/ZHIMulToolkit/releases/download/v0.2.0/ZHIMulToolkit.exe", update.ExecutableUri!.AbsoluteUri);
        Assert.Equal("https://github.com/example/ZHIMulToolkit/releases/download/v0.2.0/SHA256SUMS.txt", update.ChecksumUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("v0.1.0", false, false)]
    [InlineData("v0.2.0-beta.1", false, true)]
    [InlineData("v0.2.0", true, false)]
    public async Task UpdateCheckerIgnoresSameVersionDraftAndPrerelease(string tag, bool draft, bool prerelease)
    {
        var json = $$"""
            {
              "tag_name": "{{tag}}",
              "html_url": "https://github.com/example/ZHIMulToolkit/releases/tag/{{tag}}",
              "draft": {{draft.ToString().ToLowerInvariant()}},
              "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
              "assets": []
            }
            """;
        using var client = CreateClient(json);
        var checker = new UpdateChecker(client);

        var update = await checker.CheckAsync("example/ZHIMulToolkit", "0.1.0");

        Assert.Null(update);
    }

    [Fact]
    public async Task ReleaseClientRejectsNonHttpsReleaseLinks()
    {
        const string json = """
            {
              "tag_name": "v0.2.0",
              "html_url": "http://example.com/release",
              "draft": false,
              "prerelease": false,
              "assets": []
            }
            """;
        using var client = CreateClient(json);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.GetLatestStableReleaseAsync("example/ZHIMulToolkit"));
    }

    private static GitHubReleaseClient CreateClient(string responseBody)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
        return new GitHubReleaseClient(new HttpClient(handler, disposeHandler: true));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
