namespace ZHIMulToolkit.Core.Updates;

public sealed record AvailableUpdate(
    SemanticVersion Version,
    string TagName,
    Uri ReleaseUri,
    Uri? ExecutableUri,
    Uri? ChecksumUri);

public sealed class UpdateChecker(GitHubReleaseClient releaseClient)
{
    public async Task<AvailableUpdate?> CheckAsync(
        string repository,
        string installedVersionText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(releaseClient);
        var installedVersion = SemanticVersion.Parse(installedVersionText);
        var release = await releaseClient.GetLatestStableReleaseAsync(repository, cancellationToken).ConfigureAwait(false);
        if (release.IsDraft || release.IsPrerelease)
        {
            return null;
        }

        var releaseVersion = SemanticVersion.Parse(release.TagName);
        if (releaseVersion.CompareTo(installedVersion) <= 0)
        {
            return null;
        }

        var executable = release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, "ZHIMulToolkit.exe", StringComparison.OrdinalIgnoreCase));
        var checksum = release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, "SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
        return new AvailableUpdate(releaseVersion, release.TagName, release.ReleaseUri, executable?.DownloadUri, checksum?.DownloadUri);
    }
}
