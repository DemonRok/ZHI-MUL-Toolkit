using System.Security.Cryptography;
using System.Text;
using Xunit;
using ZHIMulToolkit.Core.Updates;

namespace ZHIMulToolkit.Tests;

public sealed class UpdateTests
{
    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta", -1)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11", -1)]
    [InlineData("1.0.0-rc.1", "1.0.0", -1)]
    [InlineData("1.2.3+one", "1.2.3+two", 0)]
    [InlineData("v2.0.0", "1.99.99", 1)]
    public void SemanticVersionUsesSemVerPrecedence(string leftText, string rightText, int expectedSign)
    {
        var left = SemanticVersion.Parse(leftText);
        var right = SemanticVersion.Parse(rightText);

        Assert.Equal(expectedSign, Math.Sign(left.CompareTo(right)));
    }

    [Theory]
    [InlineData("1.02.3")]
    [InlineData("1.2")]
    [InlineData("1.2.3-01")]
    [InlineData("v1.2.3-hello_")]
    public void SemanticVersionRejectsInvalidText(string value)
    {
        Assert.False(SemanticVersion.TryParse(value, out _));
    }

    [Fact]
    public void RepositoryNameMustContainAnOwnerAndRepository()
    {
        Assert.True(GitHubReleaseClient.IsRepositoryName("mauro/ZHIMulToolkit"));
        Assert.False(GitHubReleaseClient.IsRepositoryName("ZHIMulToolkit"));
        Assert.False(GitHubReleaseClient.IsRepositoryName("owner/repo/extra"));
    }

    [Fact]
    public void ChecksumManifestFindsNamedExecutable()
    {
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var manifest = $"{hash}  ZHIMulToolkit.exe\n{hash}  SHA256SUMS.txt\n";

        Assert.Equal(hash.ToUpperInvariant(), Sha256Checksum.ReadExpectedHash(manifest, "ZHIMulToolkit.exe"));
        Assert.Null(Sha256Checksum.ReadExpectedHash(manifest, "other.exe"));
    }

    [Fact]
    public async Task ChecksumVerifierDetectsValidAndInvalidHashes()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ZHIMulToolkit.exe");
        var contents = Encoding.UTF8.GetBytes("sample executable bytes");
        await File.WriteAllBytesAsync(path, contents);
        var valid = Convert.ToHexString(SHA256.HashData(contents));

        Assert.True(await Sha256Checksum.VerifyFileAsync(path, valid));
        Assert.False(await Sha256Checksum.VerifyFileAsync(path, new string('0', 64)));
    }
}
