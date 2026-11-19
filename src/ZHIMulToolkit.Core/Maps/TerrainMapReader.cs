using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;

namespace ZHIMulToolkit.Core.Maps;

public sealed record TerrainTile(ushort Graphic, sbyte Elevation);

public sealed class TerrainMapBlock
{
    internal TerrainMapBlock(uint header, byte[] rawData, TerrainTile[] tiles)
    {
        Header = header;
        RawData = rawData;
        Tiles = Array.AsReadOnly(tiles);
    }

    public uint Header { get; }

    public ReadOnlyMemory<byte> RawData { get; }

    public IReadOnlyList<TerrainTile> Tiles { get; }
}

public sealed class TerrainMapReader : IDisposable
{
    public const int BlockSize = 196;
    public const int HeaderSize = 4;
    public const int TileCount = 64;
    public const int TileRecordSize = 3;

    private readonly FileStream _stream;
    private readonly object _readLock = new();

    public TerrainMapReader(string path, MapDimensions? dimensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);

        if (_stream.Length % BlockSize != 0)
        {
            _stream.Dispose();
            throw new InvalidDataException("Terrain MUL length is not a whole number of 196-byte blocks.");
        }

        BlockCount = _stream.Length / BlockSize;
        if (dimensions is not null && dimensions.BlockCount != BlockCount)
        {
            _stream.Dispose();
            throw new InvalidDataException($"Terrain MUL has {BlockCount} blocks; the selected dimensions require {dimensions.BlockCount}.");
        }

        Dimensions = dimensions;
    }

    public long BlockCount { get; }

    public MapDimensions? Dimensions { get; }

    public TerrainMapBlock ReadBlock(long blockIndex)
    {
        if ((ulong)blockIndex >= (ulong)BlockCount)
        {
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        }

        var bytes = new byte[BlockSize];
        var offset = checked(blockIndex * BlockSize);
        int read;
        lock (_readLock)
        {
            read = RandomAccess.Read(_stream.SafeFileHandle, bytes, offset);
        }

        if (read != BlockSize)
        {
            throw new EndOfStreamException("The terrain block was truncated while it was being read.");
        }

        return TerrainBlockParser.Parse(bytes);
    }

    public void Dispose() => _stream.Dispose();
}

internal static class TerrainBlockParser
{
    public static TerrainMapBlock Parse(byte[] bytes)
    {
        if (bytes.Length != TerrainMapReader.BlockSize)
        {
            throw new InvalidDataException("A terrain block must contain exactly 196 bytes.");
        }

        var header = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, TerrainMapReader.HeaderSize));
        var tiles = new TerrainTile[TerrainMapReader.TileCount];
        for (var tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
        {
            var tileOffset = TerrainMapReader.HeaderSize + tileIndex * TerrainMapReader.TileRecordSize;
            var graphic = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(tileOffset, sizeof(ushort)));
            var elevation = unchecked((sbyte)bytes[tileOffset + sizeof(ushort)]);
            tiles[tileIndex] = new TerrainTile(graphic, elevation);
        }

        return new TerrainMapBlock(header, bytes, tiles);
    }
}
