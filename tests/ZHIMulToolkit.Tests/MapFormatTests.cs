using System.Buffers.Binary;
using Xunit;
using ZHIMulToolkit.Core.Maps;

namespace ZHIMulToolkit.Tests;

public sealed class MapFormatTests
{
    [Fact]
    public void BlockCoordinatesUseColumnMajorBlockOrder()
    {
        var dimensions = MapDimensions.Create(16, 24);

        var index = dimensions.GetBlockIndex(tileX: 8, tileY: 16);

        Assert.Equal(5, index);
        Assert.Equal((1, 2), dimensions.GetBlockCoordinates(index));
    }

    [Fact]
    public void DimensionsMustBeMultiplesOfEightTiles()
    {
        Assert.Throws<ArgumentException>(() => MapDimensions.Create(15, 16));
    }

    [Fact]
    public void DiscoveryFindsAllKnownFileFamiliesAndUnrestrictedMapIds()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllBytes(Path.Combine(directory.Path, "MAP12.MUL"), []);
        File.WriteAllBytes(Path.Combine(directory.Path, "mapdifl12.mul"), []);
        File.WriteAllBytes(Path.Combine(directory.Path, "staidx12.mul"), []);
        File.WriteAllBytes(Path.Combine(directory.Path, "stadifi12.mul"), []);
        File.WriteAllBytes(Path.Combine(directory.Path, "notes.txt"), []);

        var maps = MapDiscovery.Discover(directory.Path);

        var map = Assert.Single(maps);
        Assert.Equal(12, map.MapId);
        Assert.True(map.Has(MapFileKind.Terrain));
        Assert.True(map.Has(MapFileKind.TerrainDiffLookup));
        Assert.True(map.Has(MapFileKind.StaticIndex));
        Assert.True(map.Has(MapFileKind.StaticDiffIndex));
        Assert.Equal(4, map.Files.Count);
    }

    [Fact]
    public void TerrainReaderParsesHeaderAndSignedElevation()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "map0.mul");
        var block = new byte[TerrainMapReader.BlockSize];
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(0, 4), 0x12345678);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(4, 2), 0x3456);
        block[6] = 0xFE;
        File.WriteAllBytes(path, block);

        using var reader = new TerrainMapReader(path, MapDimensions.Create(8, 8));
        var parsed = reader.ReadBlock(0);

        Assert.Equal(1, reader.BlockCount);
        Assert.Equal(0x12345678u, parsed.Header);
        Assert.Equal(64, parsed.Tiles.Count);
        Assert.Equal(new TerrainTile(0x3456, -2), parsed.Tiles[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadBlock(1));
    }

    [Fact]
    public void TerrainReaderRejectsTruncatedAndDimensionMismatchFiles()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "map0.mul");
        File.WriteAllBytes(path, new byte[TerrainMapReader.BlockSize - 1]);
        Assert.Throws<InvalidDataException>(() => new TerrainMapReader(path));

        File.WriteAllBytes(path, new byte[TerrainMapReader.BlockSize]);
        Assert.Throws<InvalidDataException>(() => new TerrainMapReader(path, MapDimensions.Create(16, 8)));
    }

    [Fact]
    public void TerrainDiffReaderMapsPatchOrderToBaseBlockIdentifiers()
    {
        using var directory = new TemporaryDirectory();
        var dataPath = Path.Combine(directory.Path, "mapdif0.mul");
        var lookupPath = Path.Combine(directory.Path, "mapdifl0.mul");
        var data = new byte[TerrainMapReader.BlockSize * 2];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(TerrainMapReader.BlockSize + 4, 2), 0x1234);
        File.WriteAllBytes(dataPath, data);
        var lookup = new byte[sizeof(uint) * 2];
        BinaryPrimitives.WriteUInt32LittleEndian(lookup.AsSpan(0, 4), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(lookup.AsSpan(4, 4), 1);
        File.WriteAllBytes(lookupPath, lookup);

        using var reader = new TerrainDiffReader(dataPath, lookupPath, baseBlockCount: 6);
        var entries = reader.Entries;
        var patchedBlock = reader.ReadPatchBlock(1);

        Assert.Equal(new TerrainDiffEntry(5, 0), entries[0]);
        Assert.Equal(new TerrainDiffEntry(1, 1), entries[1]);
        Assert.Equal((ushort)0x1234, patchedBlock.Tiles[0].Graphic);
    }

    [Fact]
    public void TerrainDiffReaderRejectsDuplicateAndOutOfRangeBaseReferences()
    {
        using var directory = new TemporaryDirectory();
        var dataPath = Path.Combine(directory.Path, "mapdif0.mul");
        var lookupPath = Path.Combine(directory.Path, "mapdifl0.mul");
        File.WriteAllBytes(dataPath, new byte[TerrainMapReader.BlockSize * 2]);
        var lookup = new byte[sizeof(uint) * 2];
        BinaryPrimitives.WriteUInt32LittleEndian(lookup.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(lookup.AsSpan(4, 4), 1);
        File.WriteAllBytes(lookupPath, lookup);
        Assert.Throws<InvalidDataException>(() => new TerrainDiffReader(dataPath, lookupPath, baseBlockCount: 4));

        BinaryPrimitives.WriteUInt32LittleEndian(lookup.AsSpan(4, 4), 4);
        File.WriteAllBytes(lookupPath, lookup);
        Assert.Throws<InvalidDataException>(() => new TerrainDiffReader(dataPath, lookupPath, baseBlockCount: 4));
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ZHIMulToolkitTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}
