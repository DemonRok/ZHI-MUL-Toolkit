using System.Buffers.Binary;

namespace ZHIMulToolkit.Core.Maps;

public sealed record TerrainDiffEntry(uint BaseBlockIndex, int PatchOrdinal);

public sealed class TerrainDiffReader : IDisposable
{
    private readonly FileStream _dataStream;
    private readonly TerrainDiffEntry[] _entries;
    private readonly object _readLock = new();

    public TerrainDiffReader(string dataPath, string lookupPath, long? baseBlockCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupPath);

        _dataStream = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
        try
        {
            if (_dataStream.Length % TerrainMapReader.BlockSize != 0)
            {
                throw new InvalidDataException("Terrain DIF data length is not a whole number of 196-byte blocks.");
            }

            var patchCount = checked((int)(_dataStream.Length / TerrainMapReader.BlockSize));
            var lookupInfo = new FileInfo(lookupPath);
            if (!lookupInfo.Exists || lookupInfo.Length % sizeof(uint) != 0)
            {
                throw new InvalidDataException("Terrain DIF lookup length is not a whole number of 32-bit block identifiers.");
            }

            var lookupCount = checked((int)(lookupInfo.Length / sizeof(uint)));
            if (lookupCount != patchCount)
            {
                throw new InvalidDataException($"Terrain DIF contains {patchCount} blocks but its lookup contains {lookupCount} identifiers.");
            }

            var bytes = File.ReadAllBytes(lookupPath);
            _entries = new TerrainDiffEntry[lookupCount];
            var seen = new HashSet<uint>();
            for (var ordinal = 0; ordinal < lookupCount; ordinal++)
            {
                var blockIndex = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(ordinal * sizeof(uint), sizeof(uint)));
                if (baseBlockCount is not null && blockIndex >= baseBlockCount.Value)
                {
                    throw new InvalidDataException($"Terrain DIF references block {blockIndex}, outside the base map.");
                }

                if (!seen.Add(blockIndex))
                {
                    throw new InvalidDataException($"Terrain DIF references block {blockIndex} more than once.");
                }

                _entries[ordinal] = new TerrainDiffEntry(blockIndex, ordinal);
            }
        }
        catch
        {
            _dataStream.Dispose();
            throw;
        }
    }

    public IReadOnlyList<TerrainDiffEntry> Entries => _entries;

    public TerrainMapBlock ReadPatchBlock(int patchOrdinal)
    {
        if ((uint)patchOrdinal >= (uint)_entries.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(patchOrdinal));
        }

        var bytes = new byte[TerrainMapReader.BlockSize];
        var offset = checked((long)patchOrdinal * TerrainMapReader.BlockSize);
        int read;
        lock (_readLock)
        {
            read = RandomAccess.Read(_dataStream.SafeFileHandle, bytes, offset);
        }

        if (read != bytes.Length)
        {
            throw new EndOfStreamException("The terrain DIF block was truncated while it was being read.");
        }

        return TerrainBlockParser.Parse(bytes);
    }

    public void Dispose() => _dataStream.Dispose();
}
