using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ProjectMaelstrom.Utilities;

/// <summary>
/// Detailed metadata for an entry within a KingsIsle WAD archive.
/// Matches libkiwad and wizwad file entry structures.
/// </summary>
public record WadEntryInfo(
    string Path,
    int Offset,
    int Size,
    int CompressedSize,
    bool IsCompressed,
    int Checksum);

/// <summary>
/// Native C# parser and reader for KingsIsle WAD archives (.wad).
/// Ported from libkiwad (libkiwad-master) and wizwad (wizwad-main).
/// Parses KIWAD v1 and v2 headers, entry journals, and decompresses zlib streams directly.
/// </summary>
public sealed class WadArchiveReader : IDisposable
{
    private const string KiwadMagic = "KIWAD";
    private readonly FileStream _fileStream;
    private readonly BinaryReader _reader;
    private readonly Dictionary<string, WadEntryInfo> _entries = new(StringComparer.OrdinalIgnoreCase);

    public string FilePath { get; }
    public int Version { get; private set; }
    public int EntryCount { get; private set; }
    public IReadOnlyDictionary<string, WadEntryInfo> Entries => _entries;

    public WadArchiveReader(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("WAD file not found.", filePath);
        }

        FilePath = filePath;
        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _reader = new BinaryReader(_fileStream, Encoding.ASCII, leaveOpen: false);

        ParseHeaderAndJournal();
    }

    private void ParseHeaderAndJournal()
    {
        byte[] magicBytes = _reader.ReadBytes(5);
        string magic = Encoding.ASCII.GetString(magicBytes);
        if (!string.Equals(magic, KiwadMagic, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Invalid WAD header magic: '{magic}'. Expected '{KiwadMagic}'.");
        }

        Version = _reader.ReadInt32();
        EntryCount = _reader.ReadInt32();

        if (Version >= 2)
        {
            // Skip 1 byte spacing for v2+ format
            _reader.ReadByte();
        }

        for (int i = 0; i < EntryCount; i++)
        {
            int offset = _reader.ReadInt32();
            int size = _reader.ReadInt32();
            int compressedSize = _reader.ReadInt32();
            bool isCompressed = _reader.ReadBoolean();
            int checksum = _reader.ReadInt32();
            int pathLength = _reader.ReadInt32();
            byte[] pathBytes = _reader.ReadBytes(pathLength);
            string entryPath = Encoding.ASCII.GetString(pathBytes).TrimEnd('\0');

            var entry = new WadEntryInfo(entryPath, offset, size, compressedSize, isCompressed, checksum);
            _entries[entryPath] = entry;
        }
    }

    /// <summary>
    /// Reads and decompresses (if required) an entry's raw data bytes.
    /// </summary>
    public byte[] ExtractEntry(WadEntryInfo entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _fileStream.Seek(entry.Offset, SeekOrigin.Begin);
        int readSize = entry.IsCompressed ? entry.CompressedSize : entry.Size;
        byte[] data = _reader.ReadBytes(readSize);

        if (!entry.IsCompressed)
        {
            return data;
        }

        using var memoryStream = new MemoryStream(data);
        using var zlibStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
        using var outStream = new MemoryStream(entry.Size);
        zlibStream.CopyTo(outStream);
        return outStream.ToArray();
    }

    public void Dispose()
    {
        _reader.Dispose();
        _fileStream.Dispose();
    }
}
