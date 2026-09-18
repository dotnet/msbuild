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
    private readonly string _outputPath;
    private readonly BinaryLoggerParameters _loggerParameters;
    private readonly HashSet<BinaryLogRecordKind> _excludedKinds;

    private FilteredBinlogReplay(string inputPath, string outputPath, BinaryLoggerParameters loggerParameters, HashSet<BinaryLogRecordKind> excludedKinds)
    {
        _inputPath = inputPath;
        _outputPath = outputPath;
        _loggerParameters = loggerParameters;
        _excludedKinds = excludedKinds;
    }

    internal static FilteredBinlogReplay Create(string inputPath, CommandLineSwitches switches)
    {
        ValidateSwitches(switches);
        CommandLineSwitchException.VerifyThrow(FileUtilities.IsBinaryLogFilename(inputPath), "ReplayFilterInputRequired", inputPath);

        string[] filters = switches[CommandLineSwitches.ParameterizedSwitch.ReplayFilter];
        CommandLineSwitchException.VerifyThrow(filters.Length == 1, "DuplicateReplayFilter", "-replayFilter");
        string expression = filters[0];
        CommandLineSwitchException.VerifyThrow(expression.StartsWith("Exclude=", StringComparison.OrdinalIgnoreCase), "InvalidReplayFilter", expression);

        HashSet<BinaryLogRecordKind> excludedKinds = [];
        foreach (string entry in expression.Substring("Exclude=".Length).Split(','))
        {
            string name = entry.Trim();
            CommandLineSwitchException.VerifyThrow(
                Enum.TryParse(name, ignoreCase: true, out BinaryLogRecordKind kind)
                && string.Equals(Enum.GetName(typeof(BinaryLogRecordKind), kind), name, StringComparison.OrdinalIgnoreCase)
                && CanExclude(kind),
                "InvalidReplayFilter",
                name);
            excludedKinds.Add(kind);
        }

        CommandLineSwitchException.VerifyThrow(
            excludedKinds.Contains(BinaryLogRecordKind.ProjectEvaluationStarted) == excludedKinds.Contains(BinaryLogRecordKind.ProjectEvaluationFinished),
            "ReplayFilterEvaluationPair",
            expression);

        string[] binaryLoggerParameters = switches[CommandLineSwitches.ParameterizedSwitch.BinaryLogger];
        CommandLineSwitchException.VerifyThrow(binaryLoggerParameters.Length == 1, "ReplayFilterOutputRequired", "-binaryLogger");
        var parameters = BinaryLogger.ParseParameters(binaryLoggerParameters[0]);
        CommandLineSwitchException.VerifyThrow(
            parameters.ProjectImportsCollectionMode != BinaryLogger.ProjectImportsCollectionMode.ZipFile,
            "ReplayFilterZipNotSupported",
            binaryLoggerParameters[0]);

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(parameters.LogFilePath ?? BinaryLogger.ExtractFilePathFromParameters(binaryLoggerParameters[0]));
        }
        catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
        {
            CommandLineSwitchException.Throw("ReplayFilterInvalidOutput", binaryLoggerParameters[0], ex.Message);
            throw;
        }

        CommandLineSwitchException.VerifyThrow(!File.Exists(outputPath) && !Directory.Exists(outputPath), "ReplayFilterOutputExists", outputPath);
        return new FilteredBinlogReplay(inputPath, outputPath, parameters, excludedKinds);
    }

    private static bool CanExclude(BinaryLogRecordKind kind) => kind is
        BinaryLogRecordKind.Error or
        BinaryLogRecordKind.Warning or
        BinaryLogRecordKind.Message or
        BinaryLogRecordKind.CriticalBuildMessage or
        BinaryLogRecordKind.TaskCommandLine or
        BinaryLogRecordKind.ProjectEvaluationStarted or
        BinaryLogRecordKind.ProjectEvaluationFinished or
        BinaryLogRecordKind.ProjectImported or
        BinaryLogRecordKind.PropertyReassignment or
        BinaryLogRecordKind.UninitializedPropertyRead or
        BinaryLogRecordKind.EnvironmentVariableRead or
        BinaryLogRecordKind.PropertyInitialValueSet or
        BinaryLogRecordKind.TaskParameter or
        BinaryLogRecordKind.ResponseFileUsed or
        BinaryLogRecordKind.AssemblyLoad or
        BinaryLogRecordKind.BuildCheckMessage or
        BinaryLogRecordKind.BuildCheckWarning or
        BinaryLogRecordKind.BuildCheckError or
        BinaryLogRecordKind.BuildCheckTracing or
        BinaryLogRecordKind.BuildCheckAcquisition;

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
                (kind == CommandLineSwitches.ParameterizedSwitch.DistributedLogger && IsSdkLogger(switches[kind]))
                    || kind is CommandLineSwitches.ParameterizedSwitch.Project
                    or CommandLineSwitches.ParameterizedSwitch.ReplayFilter
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
                kind is CommandLineSwitches.ParameterlessSwitch.NoAutoResponse or CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger,
                "ReplayFilterUnsupportedSwitch",
                switches.GetParameterlessSwitchCommandLineArg(kind));
        }
    }

    private static bool IsSdkLogger(string[] parameters)
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
        string? stagingPath = null;
        bool ownsStagingFile = false;
        try
        {
            using var binaryReader = BinaryLogReplayEventSource.OpenReader(_inputPath);
            using var reader = BinaryLogReplayEventSource.OpenBuildEventsReader(binaryReader, closeInput: false);
            cancellationToken.ThrowIfCancellationRequested();

            string directory = Path.GetDirectoryName(_outputPath)!;
            Directory.CreateDirectory(directory);
            stagingPath = Path.Combine(directory, $".msbuild-filter-{Guid.NewGuid():N}.binlog");
            var source = new BinaryLogReplayEventSource
            {
                EventFilter = metadata => !_excludedKinds.Contains(metadata.RecordKind)
            };
            List<ILogger> initializedLoggers = [];
            bool finalized = true;
            FileStream? output = null;
            try
            {
                output = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                ownsStagingFile = true;
                foreach (ILogger logger in loggers)
                {
                    initializedLoggers.Add(logger);
                    if (logger is BinaryLogger binaryLogger)
                    {
                        // Keep the temporary path out of the logger's metadata.
                        s_initializeBinaryLogger(binaryLogger, source, output, _loggerParameters, _outputPath);
                    }
                    else if (logger is INodeLogger nodeLogger)
                    {
                        nodeLogger.Initialize(source, cpuCount);
                    }
                    else
                    {
                        logger.Initialize(source);
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
                    output?.Dispose();
                }
            }

            if (!finalized)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            // Same-directory publication must not overwrite a destination created during replay.
            File.Move(stagingPath, _outputPath);
            ownsStagingFile = false;
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
            if (ownsStagingFile)
            {
                try
                {
                    File.Delete(stagingPath!);
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                    ReportFailure(ex);
                }
            }
        }
    }

    private static void ReportFailure(Exception exception)
        => Console.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ReplayFilterFailed", exception.ToString()));
}
