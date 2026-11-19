using System.Globalization;
using System.Text.RegularExpressions;

namespace ZHIMulToolkit.Core.Maps;

public enum MapFileKind
{
    Terrain,
    TerrainDiff,
    TerrainDiffLookup,
    Statics,
    StaticIndex,
    StaticDiff,
    StaticDiffIndex,
    StaticDiffLookup
}

public sealed record DiscoveredMap(int MapId, IReadOnlyDictionary<MapFileKind, string> Files)
{
    public bool Has(MapFileKind kind) => Files.ContainsKey(kind);
}

public static class MapDiscovery
{
    private static readonly Regex FileNamePattern = new(
        "^(?<kind>mapdifl|mapdif|map|stadifl|stadifi|stadif|statics|staidx)(?<id>[0-9]+)\\.mul$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<DiscoveredMap> Discover(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The selected directory does not exist: {directory}");
        }

        var maps = new SortedDictionary<int, Dictionary<MapFileKind, string>>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.mul", SearchOption.TopDirectoryOnly))
        {
            var match = FileNamePattern.Match(Path.GetFileName(path));
            if (!match.Success || !int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var mapId))
            {
                continue;
            }

            var kind = match.Groups["kind"].Value.ToLowerInvariant() switch
            {
                "map" => MapFileKind.Terrain,
                "mapdif" => MapFileKind.TerrainDiff,
                "mapdifl" => MapFileKind.TerrainDiffLookup,
                "statics" => MapFileKind.Statics,
                "staidx" => MapFileKind.StaticIndex,
                "stadif" => MapFileKind.StaticDiff,
                "stadifi" => MapFileKind.StaticDiffIndex,
                "stadifl" => MapFileKind.StaticDiffLookup,
                _ => throw new InvalidOperationException("Unrecognized map file kind.")
            };

            if (!maps.TryGetValue(mapId, out var files))
            {
                files = new Dictionary<MapFileKind, string>();
                maps.Add(mapId, files);
            }

            files[kind] = path;
        }

        return maps.Select(pair => new DiscoveredMap(pair.Key, pair.Value)).ToArray();
    }
}
