// Copyright (c) 2026 Nenkai
// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using ACEvo.Package.Hashing;

using Syroot.BinaryData.Memory;

namespace ACEvo.Package;

public class PackFile : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private const ulong KEY = 0x9F9721A97D1135C1uL;
    private static readonly List<long> FILE_TABLE_SIZES =
    [
        0x4_000_000, // 64mb, >=0.7.
        0x2_000_000  // 32mb, <0.7.
    ];  

    private Dictionary<string, PackFileEntry> _fileTable = [];
    private Dictionary<ulong, PackFileEntry> _fileTableKeys = [];

    private FileStream _packStream;

    public PackFile(FileStream stream, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));

        _packStream = stream;
        _loggerFactory = loggerFactory;

        if (_loggerFactory is not null)
            _logger = _loggerFactory.CreateLogger(GetType().ToString());
    }

    public static PackFile Open(string file, ILoggerFactory? loggerFactory = null)
    {
        var fs = File.OpenRead(file);

        long tableSize = DetectFileTableSize(fs);
        long maxEntries = tableSize / 0x100;

        fs.Position = fs.Length - tableSize;

        byte[] fileTableBytes = new byte[tableSize];
        fs.ReadExactly(fileTableBytes);
        Xor(fileTableBytes, KEY);

        SpanReader sr = new SpanReader(fileTableBytes);

        var packFile = new PackFile(fs, loggerFactory);
        for (int i = 0; i < maxEntries; i++)
        {
            byte[] entryBytes = sr.ReadBytes(0x100);
            var entry = MemoryMarshal.Cast<byte, PackFileEntry>(entryBytes)[0];
            if (entry.PathHash == 0)
                break; // Based on logic

            unsafe
            {
                string name = Encoding.ASCII.GetString(entry.FileNameBuffer, entry.FileNameLength);
                packFile._fileTable.Add(name, entry);
                packFile._fileTableKeys.Add(entry.PathHash, entry);
            }
        }

        return packFile;
    }

    public void ExtractAll(string outputDir)
    {
        _fileCounter = 0;
        foreach (KeyValuePair<string, PackFileEntry> file in _fileTable.OrderBy(e => e.Value.FileOffset)) // Let's be friendly to hard drive and only extract sequentially.
        {
            Extract(file.Key, file.Value, outputDir);
            _fileCounter++;
        }
    }

    public bool ExtractFile(string gamePath, string outputDir)
    {
        ulong hash = HashPath(gamePath);

        if (!_fileTableKeys.TryGetValue(hash, out PackFileEntry entry))
            return false;

        Extract(gamePath, entry, outputDir);
        return true;
    }

    private int? _fileCounter;

    public void Extract(string key, PackFileEntry entry, string outputDir)
    {
        if (_fileCounter is not null)
            _logger?.LogInformation("[{fileNumber}/{fileCount}] Extracting '{path}' (0x{packSize:X} bytes)...", _fileCounter + 1, _fileTable.Count, key, entry.FileSize);
        else
            _logger?.LogInformation("Extracting '{path}' (0x{packSize:X} bytes)...", key, entry.FileSize);

        _packStream.Position = entry.FileOffset;

        string outputFile = Path.Combine(outputDir, key);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile));

        if (entry.Flags.HasFlag(FileFlags.IsDirectory))
            return;

        if (entry.FileSize > 0)
        {
            byte[] data = ArrayPool<byte>.Shared.Rent((int)entry.FileSize);
            _packStream.ReadExactly(data);

            if (entry.Flags.HasFlag(FileFlags.Encrypted))
                Xor(data.AsSpan(0, (int)entry.FileSize), KEY);

            using var outputStream = File.Create(outputFile);
            outputStream.Write(data.AsSpan(0, (int)entry.FileSize));

            ArrayPool<byte>.Shared.Return(data);
        }
        else
            File.WriteAllBytes(outputFile, []);

    }

    public void ListFiles(string outputPath, bool log = false)
    {
        using var sw = new StreamWriter(outputPath);
        foreach (var file in _fileTable.OrderBy(e => e.Key))
        {
            sw.WriteLine($"{file.Key} - offset:{file.Value.FileOffset:X8}, size:{file.Value.FileSize:X8} hash: {file.Value.PathHash} ({file.Value.Flags})");

            if (log)
            {
                _logger?.LogInformation("{path} - offset:{offset:X8}, size:{size:X8} hash:{hash:X8} ({flags})",
                    file.Key,
                    file.Value.FileOffset,
                    file.Value.FileSize,
                    file.Value.PathHash,
                    file.Value.Flags);
            }
        }
    }

    private static ulong HashPath(string path)
    {
        byte[] bytes = Encoding.Unicode.GetBytes(path.ToLower().Replace('/', '\\'));
        return FNV1A64.Hash(bytes);
    }

    private static void Xor(Span<byte> data, ulong key)
    {
        while (data.Length >= 8)
        {
            Span<ulong> ulongs = MemoryMarshal.Cast<byte, ulong>(data);
            ulongs[0] ^= key;
            data = data[8..];
        }

        while (data.Length > 0)
        {
            data[0] ^= (byte)key;
            data = data[1..];

            key >>= 8;
        }
    }

    private static long DetectFileTableSize(FileStream fs)
    {
        Span<byte> entryBuf = stackalloc byte[0x100];
        Span<byte> copyBuf = stackalloc byte[0x100];

        foreach (long tableSize in FILE_TABLE_SIZES)
        {
            fs.Position = fs.Length - tableSize;
            fs.ReadExactly(entryBuf);

            entryBuf.CopyTo(copyBuf);
            Xor(copyBuf, KEY);

            var entry = MemoryMarshal.Cast<byte, PackFileEntry>(copyBuf)[0];
            if (IsValidFile(entry))
                return tableSize;
        }

        throw new InvalidDataException("Could not detect a valid file table size.");
    }

    // Some heuristics to check if a file is valid, not perfect but should cover most cases
    private static unsafe bool IsValidFile(PackFileEntry entry)
    {
        if (entry.PathHash == 0)
            return false;

        if (entry.FileNameLength <= 0 || entry.FileNameLength > 260)
            return false;

        ReadOnlySpan<byte> strBytes = new Span<byte>(entry.FileNameBuffer, entry.FileNameLength);
        if (!Encoding.ASCII.GetString(strBytes).StartsWith("content\\"))
            return false;

        return true;
    }

    public void Dispose()
    {
        ((IDisposable)_packStream).Dispose();
        GC.SuppressFinalize(this);
    }
}
