using System.IO.Compression;
using fNbt;

namespace Aspire.Hosting.Minecraft.Integration.Tests.Helpers;

/// <summary>
/// Represents a Minecraft block state consisting of a block type and optional properties.
/// </summary>
/// <param name="Name">The namespaced block identifier (e.g. "minecraft:stone").</param>
/// <param name="Properties">Block state properties such as facing, half, waterlogged, etc.</param>
public record BlockState(string Name, IReadOnlyDictionary<string, string> Properties)
{
    /// <summary>
    /// Returns the block state in a human-readable format (e.g. "minecraft:oak_stairs[facing=east,half=top]").
    /// </summary>
    public override string ToString()
    {
        if (Properties.Count == 0)
            return Name;
        var props = string.Join(",", Properties.Select(p => $"{p.Key}={p.Value}"));
        return $"{Name}[{props}]";
    }
}

/// <summary>
/// Reads Minecraft Anvil (.mca) region files to extract block data.
/// Supports the 1.18+ world format with negative Y coordinates (Y range: -64 to 319).
/// 
/// <para><b>Anvil Format Overview:</b></para>
/// <list type="bullet">
///   <item>Region file covers 32×32 chunks (512×512 blocks in X/Z)</item>
///   <item>Header: first 4 KB = 1024 offset entries, next 4 KB = timestamps</item>
///   <item>Each offset entry: 3 bytes sector offset + 1 byte sector count</item>
///   <item>Chunk data: length prefix (4 bytes) + compression type (1 byte) + compressed NBT</item>
///   <item>Compression type 2 = zlib (standard for modern Minecraft)</item>
/// </list>
/// 
/// <para><b>Block Storage (1.18+):</b></para>
/// <list type="bullet">
///   <item>Each chunk has vertical sections (16×16×16 blocks each)</item>
///   <item>Sections indexed by Y: section -4 covers Y=-64 to -49, section 19 covers Y=304 to 319</item>
///   <item>Each section uses palette-based storage with bit-packed long arrays</item>
///   <item>Palette maps indices to block state names + properties</item>
/// </list>
/// </summary>
public sealed class AnvilRegionReader : IDisposable
{
    /// <summary>Minimum Y coordinate in the 1.18+ world format.</summary>
    public const int MinY = -64;

    /// <summary>Maximum Y coordinate in the 1.18+ world format.</summary>
    public const int MaxY = 319;

    /// <summary>Size of the MCA header offset table in bytes (1024 entries × 4 bytes).</summary>
    private const int HeaderSize = 4096;

    /// <summary>Size of one sector in an MCA file (4 KB).</summary>
    private const int SectorSize = 4096;

    /// <summary>Number of chunks per region axis (32×32 = 1024 chunks per region).</summary>
    private const int ChunksPerAxis = 32;

    /// <summary>Blocks per chunk axis.</summary>
    private const int BlocksPerChunk = 16;

    /// <summary>Lowest section Y index in 1.18+ format (Y=-64 → section -4).</summary>
    private const int MinSectionY = -4;

    /// <summary>Highest section Y index in 1.18+ format (Y=304 → section 19).</summary>
    private const int MaxSectionY = 19;

    private readonly string _filePath;
    private readonly int _regionX;
    private readonly int _regionZ;
    private readonly byte[] _header;
    private readonly FileStream _stream;
    private bool _disposed;

    private AnvilRegionReader(string filePath, int regionX, int regionZ, byte[] header, FileStream stream)
    {
        _filePath = filePath;
        _regionX = regionX;
        _regionZ = regionZ;
        _header = header;
        _stream = stream;
    }

    /// <summary>
    /// Opens a region file and reads its header. The file path must follow the
    /// <c>r.{x}.{z}.mca</c> naming convention.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the .mca file.</param>
    /// <returns>An <see cref="AnvilRegionReader"/> ready to query blocks.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when the filename does not match the expected format.</exception>
    public static AnvilRegionReader Open(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Region file not found: {filePath}", filePath);

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        // Expected: "r.{x}.{z}" after stripping .mca
        var parts = fileName.Split('.');
        if (parts.Length != 3 || parts[0] != "r"
            || !int.TryParse(parts[1], out var regionX)
            || !int.TryParse(parts[2], out var regionZ))
        {
            throw new ArgumentException(
                $"Invalid region file name '{Path.GetFileName(filePath)}'. Expected format: r.{{x}}.{{z}}.mca",
                nameof(filePath));
        }

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = new byte[HeaderSize];
        var bytesRead = stream.Read(header, 0, HeaderSize);
        if (bytesRead < HeaderSize)
        {
            stream.Dispose();
            throw new InvalidDataException(
                $"Region file header too short ({bytesRead} bytes). Expected {HeaderSize} bytes.");
        }

        return new AnvilRegionReader(filePath, regionX, regionZ, header, stream);
    }

    /// <summary>
    /// Gets the block state at the given world coordinates.
    /// </summary>
    /// <param name="worldX">World X coordinate.</param>
    /// <param name="worldY">World Y coordinate (valid range: -64 to 319).</param>
    /// <param name="worldZ">World Z coordinate.</param>
    /// <returns>
    /// The <see cref="BlockState"/> at the specified position, or <c>null</c> if the chunk
    /// is not loaded, the section is absent, or the coordinates are out of range.
    /// </returns>
    public BlockState? GetBlockAt(int worldX, int worldY, int worldZ)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (worldY < MinY || worldY > MaxY)
            return null;

        // Convert world coords to chunk coords
        var chunkX = Math.DivRem(worldX, BlocksPerChunk, out var blockX);
        var chunkZ = Math.DivRem(worldZ, BlocksPerChunk, out var blockZ);
        if (blockX < 0) { blockX += BlocksPerChunk; chunkX--; }
        if (blockZ < 0) { blockZ += BlocksPerChunk; chunkZ--; }

        // Verify this chunk belongs to our region
        var expectedRegionX = chunkX >> 5; // floor(chunkX / 32)
        var expectedRegionZ = chunkZ >> 5;
        if (expectedRegionX != _regionX || expectedRegionZ != _regionZ)
            return null;

        // Local chunk index within the region (0–31)
        var localChunkX = ((chunkX % ChunksPerAxis) + ChunksPerAxis) % ChunksPerAxis;
        var localChunkZ = ((chunkZ % ChunksPerAxis) + ChunksPerAxis) % ChunksPerAxis;

        var chunkNbt = ReadChunkNbt(localChunkX, localChunkZ);
        if (chunkNbt is null)
            return null;

        // Section Y index
        var sectionY = (worldY < 0)
            ? (worldY - 15) / BlocksPerChunk  // floor division for negatives
            : worldY / BlocksPerChunk;

        // Block Y within the section (0–15)
        var blockY = ((worldY % BlocksPerChunk) + BlocksPerChunk) % BlocksPerChunk;

        return ExtractBlockFromNbt(chunkNbt, sectionY, blockX, blockY, blockZ);
    }

    /// <summary>
    /// Reads and decompresses the NBT data for a chunk at the given local coordinates.
    /// </summary>
    private NbtCompound? ReadChunkNbt(int localChunkX, int localChunkZ)
    {
        // Offset table index: (localChunkX + localChunkZ * 32) * 4
        var headerIndex = (localChunkX + localChunkZ * ChunksPerAxis) * 4;
        var offsetField = (_header[headerIndex] << 16)
                        | (_header[headerIndex + 1] << 8)
                        | _header[headerIndex + 2];
        var sectorCount = _header[headerIndex + 3];

        if (offsetField == 0 || sectorCount == 0)
            return null; // Chunk not generated

        var byteOffset = (long)offsetField * SectorSize;
        _stream.Seek(byteOffset, SeekOrigin.Begin);

        // Read chunk header: 4-byte length + 1-byte compression type
        Span<byte> chunkHeader = stackalloc byte[5];
        if (_stream.Read(chunkHeader) < 5)
            return null;

        var length = (chunkHeader[0] << 24)
                   | (chunkHeader[1] << 16)
                   | (chunkHeader[2] << 8)
                   | chunkHeader[3];
        var compressionType = chunkHeader[4];

        if (length <= 1)
            return null;

        // Read compressed data (length includes the compression type byte)
        var compressedData = new byte[length - 1];
        var totalRead = 0;
        while (totalRead < compressedData.Length)
        {
            var read = _stream.Read(compressedData, totalRead, compressedData.Length - totalRead);
            if (read == 0) break;
            totalRead += read;
        }

        // Decompress based on type: 1 = GZip, 2 = Zlib, 3 = uncompressed
        using var compressedStream = new MemoryStream(compressedData);
        Stream decompressStream = compressionType switch
        {
            1 => new GZipStream(compressedStream, CompressionMode.Decompress),
            2 => new ZLibStream(compressedStream, CompressionMode.Decompress),
            3 => compressedStream,
            _ => throw new InvalidDataException($"Unknown chunk compression type: {compressionType}")
        };

        try
        {
            var nbtFile = new NbtFile();
            nbtFile.LoadFromStream(decompressStream, NbtCompression.None);
            return nbtFile.RootTag;
        }
        finally
        {
            if (compressionType is 1 or 2)
                decompressStream.Dispose();
        }
    }

    /// <summary>
    /// Extracts a block state from parsed chunk NBT at the given section and local block coordinates.
    /// Handles the 1.18+ palette-based block storage format.
    /// </summary>
    private static BlockState? ExtractBlockFromNbt(
        NbtCompound chunkRoot, int sectionY, int blockX, int blockY, int blockZ)
    {
        // 1.18+ format: sections are in a "sections" list
        var sections = chunkRoot.Get<NbtList>("sections");
        if (sections is null)
            return null;

        // Find the section matching our Y index
        NbtCompound? targetSection = null;
        foreach (NbtCompound section in sections)
        {
            var y = section.Get<NbtByte>("Y");
            if (y is not null && y.Value == sectionY)
            {
                targetSection = section;
                break;
            }
        }

        if (targetSection is null)
            return null;

        // Block states are in "block_states" compound
        var blockStates = targetSection.Get<NbtCompound>("block_states");
        if (blockStates is null)
            return null;

        var palette = blockStates.Get<NbtList>("palette");
        if (palette is null || palette.Count == 0)
            return null;

        // Single-entry palette means the entire section is one block type
        if (palette.Count == 1)
            return ParsePaletteEntry((NbtCompound)palette[0]);

        // Multi-entry palette: need to unpack from the data long array
        var data = blockStates.Get<NbtLongArray>("data");
        if (data is null)
            return null;

        var paletteIndex = UnpackPaletteIndex(data.Value, palette.Count, blockX, blockY, blockZ);
        if (paletteIndex < 0 || paletteIndex >= palette.Count)
            return null;

        return ParsePaletteEntry((NbtCompound)palette[paletteIndex]);
    }

    /// <summary>
    /// Parses a palette entry NBT compound into a <see cref="BlockState"/>.
    /// </summary>
    private static BlockState ParsePaletteEntry(NbtCompound entry)
    {
        var name = entry.Get<NbtString>("Name")?.Value ?? "minecraft:air";
        var properties = new Dictionary<string, string>();

        var propsCompound = entry.Get<NbtCompound>("Properties");
        if (propsCompound is not null)
        {
            foreach (var tag in propsCompound)
            {
                if (tag is NbtString strTag)
                    properties[strTag.Name!] = strTag.Value;
            }
        }

        return new BlockState(name, properties);
    }

    /// <summary>
    /// Unpacks a palette index from the bit-packed long array used in 1.18+ block storage.
    /// 
    /// <para>The storage format packs indices into 64-bit longs. The number of bits per index
    /// is determined by the palette size (minimum 4 bits). Indices do NOT span across long
    /// boundaries — unused bits at the end of each long are padding.</para>
    /// </summary>
    /// <param name="data">The packed long array from the section's block_states.data.</param>
    /// <param name="paletteSize">Number of entries in the palette.</param>
    /// <param name="blockX">Block X within section (0–15).</param>
    /// <param name="blockY">Block Y within section (0–15).</param>
    /// <param name="blockZ">Block Z within section (0–15).</param>
    /// <returns>The palette index, or -1 if the data is malformed.</returns>
    private static int UnpackPaletteIndex(long[] data, int paletteSize, int blockX, int blockY, int blockZ)
    {
        // Bits per entry: ceil(log2(paletteSize)), minimum 4
        var bitsPerEntry = Math.Max(4, (int)Math.Ceiling(Math.Log2(paletteSize)));

        // Block index within the 16×16×16 section (Y×256 + Z×16 + X)
        var blockIndex = (blockY * BlocksPerChunk + blockZ) * BlocksPerChunk + blockX;

        // How many indices fit in one 64-bit long (no spanning)
        var indicesPerLong = 64 / bitsPerEntry;

        // Which long and which position within it
        var longIndex = blockIndex / indicesPerLong;
        var indexInLong = blockIndex % indicesPerLong;

        if (longIndex >= data.Length)
            return -1;

        var mask = (1L << bitsPerEntry) - 1;
        var value = (int)((data[longIndex] >> (indexInLong * bitsPerEntry)) & mask);
        return value;
    }

    /// <summary>
    /// Determines which region file contains the given world coordinates.
    /// </summary>
    /// <param name="worldX">World X coordinate.</param>
    /// <param name="worldZ">World Z coordinate.</param>
    /// <returns>A tuple of (regionX, regionZ) identifying the region file.</returns>
    public static (int regionX, int regionZ) WorldToRegion(int worldX, int worldZ)
    {
        // region = floor(worldCoord / 512)
        var regionX = worldX >= 0 ? worldX / 512 : (worldX - 511) / 512;
        var regionZ = worldZ >= 0 ? worldZ / 512 : (worldZ - 511) / 512;
        return (regionX, regionZ);
    }

    /// <summary>
    /// Builds the expected region file path for the given world coordinates within a save directory.
    /// </summary>
    /// <param name="worldSaveDir">Path to the Minecraft world save directory (contains the "region" folder).</param>
    /// <param name="worldX">World X coordinate.</param>
    /// <param name="worldZ">World Z coordinate.</param>
    /// <returns>The full path to the region .mca file.</returns>
    public static string GetRegionFilePath(string worldSaveDir, int worldX, int worldZ)
    {
        var (rx, rz) = WorldToRegion(worldX, worldZ);
        return Path.Combine(worldSaveDir, "region", $"r.{rx}.{rz}.mca");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _stream.Dispose();
            _disposed = true;
        }
    }
}
