// Copyright (c) 2026 Nenkai
// SPDX-License-Identifier: MIT

using System.Reflection;

using System.CommandLine;

using NLog;
using NLog.Extensions.Logging;

using Microsoft.Extensions.Logging;

namespace ACEvo.Package.CLI;

public class Program
{
    public static Version? GetExecutableVersion() => Assembly.GetEntryAssembly()?.GetName().Version;

    private static ILoggerFactory _loggerFactory;
    private static Microsoft.Extensions.Logging.ILogger _logger;

    static async Task<int> Main(string[] args)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        _logger = _loggerFactory.CreateLogger<Program>();

        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"- ACEvo.Package.CLI {GetExecutableVersion():3} by Nenkai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("- https://github.com/Nenkai");
        Console.WriteLine("- https://twitter.com/Nenkaai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("");

        var unpackCommand = new Command("unpack", "Unpacks a .kspkg file.")
        {
            new Option<FileInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input .kspkg file." },
            new Option<string>("--output", ["-o"]) { Description = "Output directory. Optional, defaults to a folder named the same as the .kspkg file." },
        };
        unpackCommand.SetAction(Unpack);

        var unpackFileCommand = new Command("unpack-file", "Unpacks a specific file from a .kspkg pack.")
        {
            new Option<FileInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input .kspkg file." },
            new Option<string>("--file", ["-f"]) { Required = true, Description = "File to unpack from the .kspkg file." },
            new Option<string>("--output", ["-o"]) { Description = "Optional. Output directory." },
        };
        unpackFileCommand.SetAction(UnpackFile);

        var listFilesCommand = new Command("list-files", "List files in a .kspkg pack.")
        {
            new Option<FileInfo>("--input", aliases: ["-i"]) { Required = true, Description = "Input .kspkg file." },
        };
        listFilesCommand.SetAction(ListFiles);

        var rootCommand = new RootCommand("ACEvo.Package.CLI")
        {
            unpackCommand,
            unpackFileCommand,
            listFilesCommand,
        };

        return await rootCommand.Parse(args).InvokeAsync();
    }

    static void UnpackFile(ParseResult parseResult)
    {
        FileInfo inputFile = parseResult.GetRequiredValue<FileInfo>("--input");
        string? fileToUnpack = parseResult.GetRequiredValue<string>("--file");
        if (!inputFile.Exists)
        {
            _logger.LogError("File '{path}' does not exist", inputFile.FullName);
            return;
        }

        string? outputPath = parseResult.GetValue<string>("--output");
        if (string.IsNullOrEmpty(outputPath))
        {
            string inputFileName = Path.GetFileNameWithoutExtension(inputFile.FullName);
            outputPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputFile.FullName)), $"{inputFileName}.extracted");
        }

        try
        {
            using var pack = PackFile.Open(inputFile.FullName, _loggerFactory);

            _logger.LogInformation("Starting unpack process.");
            if (!pack.ExtractFile(fileToUnpack, outputPath))
                _logger.LogWarning("File '{file}' not found in pack.", fileToUnpack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unpack.");
        }
    }

    static void Unpack(ParseResult parseResult)
    {
        FileInfo inputFile = parseResult.GetRequiredValue<FileInfo>("--input");
        if (!inputFile.Exists)
        {
            _logger.LogError("File '{path}' does not exist", inputFile.FullName);
            return;
        }

        string? outputPath = parseResult.GetValue<string>("--output");
        if (string.IsNullOrEmpty(outputPath))
        {
            string inputFileName = Path.GetFileNameWithoutExtension(inputFile.FullName);
            outputPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputFile.FullName)), $"{inputFileName}.extracted");
        }

        try
        {
            using var pack = PackFile.Open(inputFile.FullName, _loggerFactory);

            _logger.LogInformation("Starting unpack process.");
            pack.ExtractAll(outputPath);
            _logger.LogInformation("Done.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unpack.");
        }
    }

    static void ListFiles(ParseResult parseResult)
    {
        FileInfo inputFile = parseResult.GetRequiredValue<FileInfo>("--input");
        if (!inputFile.Exists)
        {
            _logger.LogError("File '{path}' does not exist", inputFile.FullName);
            return;
        }

        try
        {
            using var pack = PackFile.Open(inputFile.FullName, _loggerFactory);

            string inputFileName = Path.GetFileNameWithoutExtension(inputFile.FullName);
            string outputPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(inputFile.FullName)), $"{inputFileName}_files.txt");
            pack.ListFiles(outputPath);
            _logger.LogInformation("Done. ({path})", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read pack.");
        }
    }
}
