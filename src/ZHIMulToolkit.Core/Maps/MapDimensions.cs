namespace ZHIMulToolkit.Core.Maps;

public sealed record MapDimensions
{
    private MapDimensions(int widthTiles, int heightTiles)
    {
        WidthTiles = widthTiles;
        HeightTiles = heightTiles;
        BlocksAcross = widthTiles / TilesPerBlock;
        BlocksDown = heightTiles / TilesPerBlock;
        BlockCount = checked((long)BlocksAcross * BlocksDown);
    }

    public const int TilesPerBlock = 8;

    public int WidthTiles { get; }

    public int HeightTiles { get; }

    public int BlocksAcross { get; }

    public int BlocksDown { get; }

    public long BlockCount { get; }

    public static MapDimensions Create(int widthTiles, int heightTiles)
    {
        if (widthTiles <= 0) throw new ArgumentOutOfRangeException(nameof(widthTiles), "Map dimensions must be positive.");
        if (heightTiles <= 0) throw new ArgumentOutOfRangeException(nameof(heightTiles), "Map dimensions must be positive.");
        if (widthTiles % TilesPerBlock != 0) throw new ArgumentException("Map width must be divisible by 8 tiles.", nameof(widthTiles));
        if (heightTiles % TilesPerBlock != 0) throw new ArgumentException("Map height must be divisible by 8 tiles.", nameof(heightTiles));

        return new MapDimensions(widthTiles, heightTiles);
    }

    public long GetBlockIndex(int tileX, int tileY)
    {
        if ((uint)tileX >= (uint)WidthTiles || (uint)tileY >= (uint)HeightTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(tileX), "Tile coordinates are outside the map.");
        }

        var blockX = tileX / TilesPerBlock;
        var blockY = tileY / TilesPerBlock;

        // POL's map reader and UO readers store blocks down each column.
        return blockY + (long)blockX * BlocksDown;
    }

    public (int BlockX, int BlockY) GetBlockCoordinates(long blockIndex)
    {
        if ((ulong)blockIndex >= (ulong)BlockCount)
        {
            throw new ArgumentOutOfRangeException(nameof(blockIndex));
        }

        return ((int)(blockIndex / BlocksDown), (int)(blockIndex % BlocksDown));
    }
}

public sealed record MapSizePreset(string Name, int WidthTiles, int HeightTiles)
{
    public MapDimensions Dimensions => MapDimensions.Create(WidthTiles, HeightTiles);

    public static IReadOnlyList<MapSizePreset> Standard { get; } =
    [
        new("Felucca / Trammel (legacy)", 6144, 4096),
        new("Felucca / Trammel (Mondain's Legacy)", 7168, 4096),
        new("Ilshenar", 2304, 1600),
        new("Malas", 2560, 2048),
        new("Tokuno", 1448, 1448),
        new("Ter Mur", 1280, 4096)
    ];
}
