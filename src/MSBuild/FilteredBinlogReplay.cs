// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.CommandLine.Experimental;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;

#if NETFRAMEWORK
using Directory = Microsoft.IO.Directory;
using File = Microsoft.IO.File;
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.CommandLine;

internal sealed class FilteredBinlogReplay
{
    // As with TerminalLogger initialization, shared source types prevent InternalsVisibleTo.
    // A delegate avoids wrapping initialization failures in TargetInvocationException.
    private static readonly Action<BinaryLogger, IEventSource, Stream, BinaryLoggerParameters, string> s_initializeBinaryLogger =
        (Action<BinaryLogger, IEventSource, Stream, BinaryLoggerParameters, string>)typeof(BinaryLogger)
            .GetMethod(nameof(BinaryLogger.Initialize), BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
                types: [typeof(IEventSource), typeof(Stream), typeof(BinaryLoggerParameters), typeof(string)], modifiers: null)!
            .CreateDelegate(typeof(Action<BinaryLogger, IEventSource, Stream, BinaryLoggerParameters, string>));

    private readonly string _inputPath;

    private sealed class Output(string path, BinaryLoggerParameters parameters)
    {
        internal string Path { get; } = path;
        internal BinaryLoggerParameters Parameters { get; } = parameters;
        internal string? StagingPath { get; set; }
        internal FileStream? Stream { get; set; }
        internal bool OwnsStagingFile { get; set; }
    }

    private FilteredBinlogReplay(string inputPath)
    {
        _inputPath = inputPath;
    }

    internal static bool IsRequested(string inputPath, CommandLineSwitches switches)
    {
        if (FileUtilities.IsBinaryLogFilename(inputPath))
        {
            foreach (string parameter in switches[CommandLineSwitches.ParameterizedSwitch.BinaryLogger])
            {
                if (BinaryLogger.ParseParameters(parameter).ExcludedEventKinds.Count != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static FilteredBinlogReplay Create(string inputPath, CommandLineSwitches switches)
    {
        ValidateSwitches(switches);
        var processed = BinaryLogger.ProcessParameters(switches[CommandLineSwitches.ParameterizedSwitch.BinaryLogger]);
        foreach (string parameter in processed.DistinctParameterSets)
        {
            CreateOutput(parameter);
        }

        return new FilteredBinlogReplay(inputPath);
    }

    private static Output CreateOutput(string parameter)
    {
        var parameters = BinaryLogger.ParseParameters(parameter);
        CommandLineSwitchException.VerifyThrow(
            parameters.ProjectImportsCollectionMode != BinaryLogger.ProjectImportsCollectionMode.ZipFile,
            "ReplayFilterZipNotSupported",
            parameter);

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(parameters.LogFilePath ?? BinaryLogger.ExtractFilePathFromParameters(parameter));
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            CommandLineSwitchException.Throw("ReplayFilterInvalidOutput", parameter, ex.Message);
            throw;
        }

        CommandLineSwitchException.VerifyThrow(!File.Exists(outputPath) && !Directory.Exists(outputPath), "ReplayFilterOutputExists", outputPath);
        return new Output(outputPath, parameters);
    }

    private static void ValidateSwitches(CommandLineSwitches switches)
    {
        foreach (CommandLineSwitches.ParameterizedSwitch kind in Enum.GetValues(typeof(CommandLineSwitches.ParameterizedSwitch)))
        {
            if (kind is CommandLineSwitches.ParameterizedSwitch.Invalid or CommandLineSwitches.ParameterizedSwitch.NumberOfParameterizedSwitches
                || !switches.IsParameterizedSwitchSet(kind))
            {
                continue;
            }

            CommandLineSwitchException.VerifyThrow(
                kind is CommandLineSwitches.ParameterizedSwitch.Project
                    or CommandLineSwitches.ParameterizedSwitch.Logger
                    or CommandLineSwitches.ParameterizedSwitch.DistributedLogger
                    or >= CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters and <= CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters9
                    or CommandLineSwitches.ParameterizedSwitch.BinaryLogger
                    or CommandLineSwitches.ParameterizedSwitch.Verbosity
                    or CommandLineSwitches.ParameterizedSwitch.ConsoleLoggerParameters
                    or CommandLineSwitches.ParameterizedSwitch.TerminalLogger
                    or CommandLineSwitches.ParameterizedSwitch.TerminalLoggerParameters
                    or CommandLineSwitches.ParameterizedSwitch.NoLogo
                    or CommandLineSwitches.ParameterizedSwitch.MaxCPUCount
                    or CommandLineSwitches.ParameterizedSwitch.NodeReuse
                    or CommandLineSwitches.ParameterizedSwitch.LowPriority,
                "ReplayFilterUnsupportedSwitch",
                switches.GetParameterizedSwitchCommandLineArg(kind));
        }

        foreach (CommandLineSwitches.ParameterlessSwitch kind in Enum.GetValues(typeof(CommandLineSwitches.ParameterlessSwitch)))
        {
            if (kind is CommandLineSwitches.ParameterlessSwitch.Invalid or CommandLineSwitches.ParameterlessSwitch.NumberOfParameterlessSwitches
                || !switches.IsParameterlessSwitchSet(kind))
            {
                continue;
            }

            CommandLineSwitchException.VerifyThrow(
                kind is CommandLineSwitches.ParameterlessSwitch.NoAutoResponse
                    or CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger
                    or >= CommandLineSwitches.ParameterlessSwitch.FileLogger and <= CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger,
                "ReplayFilterUnsupportedSwitch",
                switches.GetParameterlessSwitchCommandLineArg(kind));
        }
    }

    internal static bool IsSdkLogger(string[] parameters)
    {
        if (parameters.Length != 1)
        {
            return false;
        }

        string assemblyPath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "dotnet.dll");
        string logger = $"Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildLogger,{assemblyPath}"
            + $"*Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildForwardingLogger,{assemblyPath}";
        return string.Equals(QuotingUtilities.Unquote(parameters[0]), logger,
            NativeMethodsShared.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    internal bool Replay(ILogger[] loggers, int cpuCount, CancellationToken cancellationToken)
    {
        Dictionary<BinaryLogger, Output> outputs = [];
        try
        {
            using var binaryReader = BinaryLogReplayEventSource.OpenReader(_inputPath);
            using var reader = BinaryLogReplayEventSource.OpenBuildEventsReader(binaryReader, closeInput: false);
            cancellationToken.ThrowIfCancellationRequested();

            HashSet<string> outputPaths = new(NativeMethodsShared.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (ILogger logger in loggers)
            {
                if (logger is BinaryLogger binaryLogger)
                {
                    Output output = CreateOutput(binaryLogger.Parameters);
                    CommandLineSwitchException.VerifyThrow(outputPaths.Add(output.Path), "ReplayFilterOutputExists", output.Path);
                    outputs.Add(binaryLogger, output);
                }
            }

            var source = new BinaryLogReplayEventSource();
            List<ILogger> initializedLoggers = [];
            bool finalized = true;
            try
            {
                foreach (ILogger logger in loggers)
                {
                    if (logger is BinaryLogger binaryLogger)
                    {
                        Output output = outputs[binaryLogger];
                        string directory = Path.GetDirectoryName(output.Path)!;
                        Directory.CreateDirectory(directory);
                        output.StagingPath = Path.Combine(directory, $".msbuild-filter-{Guid.NewGuid():N}.binlog");
                        output.Stream = new FileStream(output.StagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        output.OwnsStagingFile = true;
                        // Keep the temporary path out of the logger's metadata.
                        initializedLoggers.Add(logger);
                        s_initializeBinaryLogger(binaryLogger, source, output.Stream, output.Parameters, output.Path);
                    }
                    else
                    {
                        initializedLoggers.Add(logger);
                        if (logger is INodeLogger nodeLogger)
                        {
                            nodeLogger.Initialize(source, cpuCount);
                        }
                        else
                        {
                            logger.Initialize(source);
                        }
                    }
                }

                source.Replay(reader, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                try
                {
                    for (int i = initializedLoggers.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            initializedLoggers[i].Shutdown();
                        }
                        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                        {
                            ReportFailure(ex);
                            finalized = false;
                        }
                    }
                }
                finally
                {
                    foreach (Output output in outputs.Values)
                    {
                        try
                        {
                            output.Stream?.Dispose();
                        }
                        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                        {
                            ReportFailure(ex);
                            finalized = false;
                        }
                    }
                }
            }

            if (!finalized)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Same-directory publication must not overwrite a destination created during replay.
            foreach (Output output in outputs.Values)
            {
                File.Move(output.StagingPath!, output.Path);
                output.OwnsStagingFile = false;
            }
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine(ResourceUtilities.GetResourceString("ReplayFilterCanceled"));
            return false;
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            ReportFailure(ex);
            return false;
        }
        finally
        {
            foreach (Output output in outputs.Values)
            {
                if (output.OwnsStagingFile)
                {
                    try
                    {
                        File.Delete(output.StagingPath!);
                    }
                    catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                    {
                        ReportFailure(ex);
                    }
                }
            }
        }
    }

    private static void ReportFailure(Exception exception)
        => Console.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ReplayFilterFailed", exception.ToString()));
}
