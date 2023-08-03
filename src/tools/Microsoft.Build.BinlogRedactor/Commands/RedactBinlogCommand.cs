// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BinlogRedactor.BinaryLog;
using Microsoft.Build.BinlogRedactor.IO;
using Microsoft.Build.BinlogRedactor.Reporting;
using Microsoft.Build.BinlogRedactor.Utils;
using Microsoft.Extensions.Logging;

namespace Microsoft.Build.BinlogRedactor.Commands;

internal sealed class RedactBinlogCommand : ExecutableCommand<RedactBinlogCommandArgs, RedactBinlogCommandHandler>
{
    private const string CommandName = "redact-binlog";

    private readonly Option<string[]> _passwordsToRedactOption = new(new [] {"--password", "-p"})
    {
        Description = "Password or other sensitive data to be redacted from binlog. Multiple options are supported.",
        Arity = new ArgumentArity(1, 1000),
        IsRequired = true,
    };

    private readonly Option<string> _inputOption = new(new[] { "--input", "-i" })
    {
        Description = "Input binary log file name. Or a directory to inspect for all existing binlogs. If not specified current directory is assumed.",
        IsRequired = false,
    };

    private readonly Option<string> _outputFileOption = new(new[] { "--output", "-o" })
    {
        Description = "Output binary log file name. If not specified, replaces the input file in place - overwrite option needs to be specified in such case.",
        IsRequired = false,
    };

    private readonly Option<bool> _overWriteOption = new(new[] { "--overwrite", "-f" })
    {
        Description = "Replace the output file if it already exists. Replace the input file if the output file is not specified.",
    };

    private readonly Option<bool> _recurseOption = new(new[] { "--recurse", "-r" })
    {
        Description = "Recurse given path (or current dir if none) for all binlogs. Applies only when single input file is not specified.",
    };

    private readonly Option<bool> _dryRunOption = new(new[] { "--dryrun" })
    {
        Description = "Performs the operation in-memory and outputs what would be performed.",
    };

    private readonly Option<bool> _logSecretsOption = new(new[] { "--logsecrets"})
    {
        Description = "Logs what secrets have been detected and replaced. This should be used only for test/troubleshooting purposes!",
    };

    public RedactBinlogCommand() :
        base(CommandName, "Provides ability to redact sensitive data from MSBuild binlogs (https://aka.ms/binlog-redactor).")
    {
        AddOption(_passwordsToRedactOption);
        AddOption(_inputOption);
        AddOption(_outputFileOption);
        AddOption(_overWriteOption);
        AddOption(_dryRunOption);
        AddOption(_recurseOption);
        AddOption(_logSecretsOption);
    }

    protected internal override RedactBinlogCommandArgs ParseContext(ParseResult parseResult)
    {
        return new RedactBinlogCommandArgs(
            parseResult.GetValueForOption(_passwordsToRedactOption),
            parseResult.GetValueForOption(_inputOption),
            parseResult.GetValueForOption(_outputFileOption),
            parseResult.GetValueForOption(_dryRunOption),
            parseResult.GetValueForOption(_overWriteOption),
            parseResult.GetValueForOption(_recurseOption),
            parseResult.GetValueForOption(_logSecretsOption));
    }
}


internal sealed class RedactBinlogCommandHandler : ICommandExecutor<RedactBinlogCommandArgs>
{
    private readonly ILogger<RedactBinlogCommandHandler> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly IBinlogProcessor _binlogProcessor;

    public RedactBinlogCommandHandler(
        ILogger<RedactBinlogCommandHandler> logger,
        IFileSystem fileSystem,
        IBinlogProcessor binlogProcessor)
    {
        _logger = logger;
        _fileSystem = fileSystem;
        _binlogProcessor = binlogProcessor;
    }

    public async Task<BinlogRedactorErrorCode> ExecuteAsync(
        RedactBinlogCommandArgs args,
        CancellationToken cancellationToken)
    {
        if (args.DryRun ?? false)
        {
            throw new BinlogRedactorException("Dry run is not supported yet.",
                BinlogRedactorErrorCode.NotYetImplementedScenario);
        }

        if (args.LogDetectedSecrets ?? false)
        {
            throw new BinlogRedactorException("Secrets logging is not supported yet.",
                BinlogRedactorErrorCode.NotYetImplementedScenario);
        }

        if (args.PasswordsToRedact == null || args.PasswordsToRedact.Length == 0)
        {
            throw new BinlogRedactorException(
                "At least one password to redact must be specified.",
                BinlogRedactorErrorCode.NotEnoughInformationToProceed);
        }

        if (string.IsNullOrEmpty(args.OutputFileName) && !(args.OverWrite ?? false))
        {
            throw new BinlogRedactorException(
                "Output file must be specified if overwrite in place is not requested.",
                BinlogRedactorErrorCode.NotEnoughInformationToProceed);
        }

        string[] inputFiles = GetInputFiles(args.InputPath, args.Recurse ?? false);
        bool hasMultipleFiles = inputFiles.Length > 1;

        if (hasMultipleFiles)
        {
            _logger.LogInformation("Found {count} binlog files. Will redact secrets in all. (found files: {files})",
                inputFiles.Length, inputFiles.ToCsvString());
        }

        int fileOrderCount = 0;
        foreach (string inputFile in inputFiles)
        {
            string outputFile;
            if (string.IsNullOrEmpty(args.OutputFileName))
            {
                outputFile = inputFile;
            }
            else
            {
                outputFile = args.OutputFileName + (hasMultipleFiles ? (fileOrderCount++).ToString("D2") : null);
            }

            var result = await RedactWorker(inputFile, outputFile, args, cancellationToken);

            // TODO: should we continue if there was an error?
            if (result != BinlogRedactorErrorCode.Success)
            {
                if (fileOrderCount != inputFiles.Length)
                {
                    _logger.LogInformation("Skipping redacting of remaining logs due to encountered error.");
                }

                return result;
            }
        }

        return BinlogRedactorErrorCode.Success;
    }

    private async Task<BinlogRedactorErrorCode> RedactWorker(
        string inputFile,
        string outputFile,
        RedactBinlogCommandArgs args,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Redacting binlog {inputFile} to {outputFile} ({size} KB)", inputFile, outputFile, _fileSystem.GetFileSizeInBytes(inputFile) / 1024);

        bool replaceInPlace = inputFile.Equals(outputFile, StringComparison.CurrentCulture);
        if (replaceInPlace)
        {
            outputFile = Path.GetFileName(Path.GetTempFileName()) + ".binlog";
        }

        if ((args.OverWrite ?? false) && _fileSystem.FileExists(outputFile))
        {
            throw new BinlogRedactorException(
                $"Requested output file [{outputFile}] exists, while overwrite option was not specified.",
                BinlogRedactorErrorCode.FileSystemWriteFailed);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        var result = await _binlogProcessor.ProcessBinlog(inputFile, outputFile,
            new SimpleSensitiveDataProcessor(args.PasswordsToRedact!), cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation("Redacting done. Duration: {duration}", stopwatch.Elapsed);

        if (replaceInPlace)
        {
            _fileSystem.ReplaceFile(outputFile, inputFile);
        }

        return result;
    }

    private string[] GetInputFiles(string? inputPath, bool recurse)
    {
        string dirToSearch = inputPath ?? ".";

        if (!string.IsNullOrEmpty(inputPath) && !_fileSystem.DirectoryExists(inputPath))
        {
            if (!_fileSystem.FileExists(inputPath))
            {
                throw new BinlogRedactorException($"Input path [{inputPath}] does not exist.",
                    BinlogRedactorErrorCode.InvalidData);
            }

            return new[] { inputPath };
        }

        string[] binlogs = _fileSystem.EnumerateFiles(dirToSearch, "*.binlog",
            new EnumerationOptions() { IgnoreInaccessible = true, RecurseSubdirectories = recurse, }).ToArray();

        if (binlogs.Length == 0)
        {
            throw new BinlogRedactorException(
                $"No binlog file found in the current directory. Please specify the input file explicitly.",
                BinlogRedactorErrorCode.NotEnoughInformationToProceed);
        }

        return binlogs;
    }

    private string GetInputFile(string? inputFileName)
    {
        if (!string.IsNullOrEmpty(inputFileName))
        {
            if (!_fileSystem.FileExists(inputFileName))
            {
                throw new BinlogRedactorException($"Input file [{inputFileName}] does not exist.",
                    BinlogRedactorErrorCode.InvalidData);
            }

            return inputFileName;
        }

        var binlogs = _fileSystem.EnumerateFiles(".", "*.binlog",
            new EnumerationOptions() { IgnoreInaccessible = true, RecurseSubdirectories = false, }).ToList();

        if (binlogs.Count > 1)
        {
            throw new BinlogRedactorException(
                $"Multiple binlog files found in the current directory [{binlogs.ToCsvString()}]. Please specify the input file explicitly.",
                BinlogRedactorErrorCode.NotEnoughInformationToProceed);
        }

        if (binlogs.Count == 0)
        {
            throw new BinlogRedactorException(
                $"No binlog file found in the current directory. Please specify the input file explicitly.",
                BinlogRedactorErrorCode.NotEnoughInformationToProceed);
        }

        return binlogs[0];
    }
}
