using System.Numerics;
using System.Text.RegularExpressions;

namespace ZHIMulToolkit.Core.Updates;

public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private static readonly Regex Pattern = new(
        "^v?(?<major>0|[1-9][0-9]*)\\.(?<minor>0|[1-9][0-9]*)\\.(?<patch>0|[1-9][0-9]*)(?:-(?<pre>[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?(?:\\+(?<build>[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private SemanticVersion(BigInteger major, BigInteger minor, BigInteger patch, string? prerelease, string? build)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        Build = build;
    }

    public BigInteger Major { get; }

    public BigInteger Minor { get; }

    public BigInteger Patch { get; }

    public string? Prerelease { get; }

    public string? Build { get; }

    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException($"'{value}' is not a valid semantic version.");
        }

        return version!;
    }

    public static bool TryParse(string? value, out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Pattern.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        var prerelease = match.Groups["pre"].Success ? match.Groups["pre"].Value : null;
        if (prerelease is not null && prerelease.Split('.').Any(identifier =>
                identifier.Length > 1 && identifier[0] == '0' && identifier.All(char.IsAsciiDigit)))
        {
            return false;
        }

        version = new SemanticVersion(
            BigInteger.Parse(match.Groups["major"].Value),
            BigInteger.Parse(match.Groups["minor"].Value),
            BigInteger.Parse(match.Groups["patch"].Value),
            prerelease,
            match.Groups["build"].Success ? match.Groups["build"].Value : null);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var comparison = Major.CompareTo(other.Major);
        if (comparison != 0) return comparison;
        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0) return comparison;
        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0) return comparison;

        if (Prerelease is null) return other.Prerelease is null ? 0 : 1;
        if (other.Prerelease is null) return -1;

        var left = Prerelease.Split('.');
        var right = other.Prerelease.Split('.');
        for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            var leftNumeric = BigInteger.TryParse(left[index], out var leftNumber) && left[index].All(char.IsAsciiDigit);
            var rightNumeric = BigInteger.TryParse(right[index], out var rightNumber) && right[index].All(char.IsAsciiDigit);

            if (leftNumeric && rightNumeric)
            {
                comparison = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric != rightNumeric)
            {
                comparison = leftNumeric ? -1 : 1;
            }
            else
            {
                comparison = string.CompareOrdinal(left[index], right[index]);
            }

            if (comparison != 0) return comparison;
        }

        return left.Length.CompareTo(right.Length);
    }

    public bool Equals(SemanticVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Prerelease);

    public override string ToString()
    {
        var value = $"{Major}.{Minor}.{Patch}";
        if (Prerelease is not null) value += $"-{Prerelease}";
        if (Build is not null) value += $"+{Build}";
        return value;
    }

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
}
