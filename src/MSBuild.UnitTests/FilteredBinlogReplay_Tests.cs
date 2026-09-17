// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.CommandLine;
using Microsoft.Build.CommandLine.Experimental;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class FilteredBinlogReplay_Tests : IDisposable
{
    private const string SourceMessage = "replay-filter source message";
    private const string SourceWarning = "replay-filter source warning";
    private const string SourceError = "replay-filter source error";
    private const string ImportedContent = "<Project><PropertyGroup><FromArchive>original</FromArchive></PropertyGroup></Project>";
    private readonly TestEnvironment _env;

    public FilteredBinlogReplay_Tests(ITestOutputHelper output)
    {
        _env = TestEnvironment.Create(output);
        MSBuildApp.Initialize();
    }

    [Theory]
    [InlineData("-replayFilter")]
    [InlineData("-replayFilter:")]
    public void ReplayFilter_MissingExpressionIsRejected(string argument)
    {
        Should.Throw<CommandLineSwitchException>(() => ParseSwitches(argument))
            .Message.ShouldContain("MSB1074");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplayFilter_RepeatedSwitchIncludingResponseFileIsRejected(bool useResponseFile)
    {
        string secondArgument = "-replayFilter:Exclude=Warning";
        if (useResponseFile)
        {
            secondArgument = $"@\"{_env.CreateFile("filter.rsp", secondArgument).Path}\"";
        }

        Should.Throw<CommandLineSwitchException>(
            () => ParseSwitches("-replayFilter:Exclude=Message", secondArgument))
            .Message.ShouldContain("MSB1075");
    }

    [Theory]
    [InlineData("Exclude=Message,Warning")]
    [InlineData("eXcLuDe=message,WARNING")]
    [InlineData("Exclude= Message , Warning , message ")]
    public void ReplayFilter_SelectionIsSharedByAllLoggersAndRetainsLifecycle(string expression)
    {
        string input = CreateInput();
        byte[] originalInput = File.ReadAllBytes(input);
        string output = OutputPath();
        CommandLineSwitches switches = ParseSwitches($"-replayFilter:\"{expression}\"", BinaryLogArgument(output, ";OmitInitialInfo"));
        switches[CommandLineSwitches.ParameterizedSwitch.ReplayFilter].ShouldBe([expression]);
        var first = new CallbackLogger();
        var second = new CallbackLogger();

        FilteredBinlogReplay.Create(input, switches)
            .Replay([new BinaryLogger(), first, second], 2, CancellationToken.None).ShouldBeTrue();

        BuildEventArgs[] expected = ReadEvents(input)
            .Where(e => e.GetType() != typeof(BuildMessageEventArgs) && e is not BuildWarningEventArgs)
            .ToArray();
        BuildEventArgs[] rewritten = ReadEvents(output);
        rewritten.Select(e => e.GetType()).ShouldBe(expected.Select(e => e.GetType()));
        rewritten.Select(e => e.Message).ShouldBe(expected.Select(e => e.Message));
        rewritten.Select(e => e.BuildEventContext?.ProjectContextId).ShouldBe(expected.Select(e => e.BuildEventContext?.ProjectContextId));
        first.Events.ShouldBe(second.Events);
        first.Events.Select(e => e.GetType()).ShouldBe(rewritten.Select(e => e.GetType()));
        first.InitializeCount.ShouldBe(1);
        first.ShutdownCount.ShouldBe(1);
        second.ShutdownCount.ShouldBe(1);
        rewritten.ShouldNotContain(e => e.GetType() == typeof(BuildMessageEventArgs));
        rewritten.ShouldNotContain(e => e is BuildWarningEventArgs);
        rewritten.OfType<CriticalBuildMessageEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<TaskCommandLineEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<BuildStartedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<BuildFinishedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<ProjectStartedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<ProjectFinishedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<TargetStartedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<TargetFinishedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<TaskStartedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<TaskFinishedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<TargetSkippedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<ProjectEvaluationStartedEventArgs>().ShouldHaveSingleItem();
        rewritten.OfType<ProjectEvaluationFinishedEventArgs>().ShouldHaveSingleItem();
        File.ReadAllBytes(input).ShouldBe(originalInput);
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("Error")]
    [InlineData("Warning")]
    [InlineData("Message")]
    [InlineData("CriticalBuildMessage")]
    [InlineData("TaskCommandLine")]
    [InlineData("ProjectEvaluationStarted,ProjectEvaluationFinished")]
    [InlineData("ProjectImported")]
    [InlineData("PropertyReassignment")]
    [InlineData("UninitializedPropertyRead")]
    [InlineData("EnvironmentVariableRead")]
    [InlineData("PropertyInitialValueSet")]
    [InlineData("TaskParameter")]
    [InlineData("ResponseFileUsed")]
    [InlineData("AssemblyLoad")]
    [InlineData("BuildCheckMessage")]
    [InlineData("BuildCheckWarning")]
    [InlineData("BuildCheckError")]
    [InlineData("BuildCheckTracing")]
    [InlineData("BuildCheckAcquisition")]
    public void ReplayFilter_AllDocumentedExclusionsAreAccepted(string kinds)
    {
        string output = OutputPath();
        CreateOperation("input.binlog", output, $"Exclude={kinds}").ShouldNotBeNull();
        File.Exists(output).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("Include=Message")]
    [InlineData("Message")]
    [InlineData("Exclude=")]
    [InlineData("Exclude= ")]
    [InlineData("Exclude=,Message")]
    [InlineData("Exclude=Message,")]
    [InlineData("Exclude=Message,,Warning")]
    [InlineData("Exclude=Message;Warning")]
    [InlineData("Exclude=9")]
    [InlineData("Exclude=-1")]
    [InlineData("Exclude=UndefinedEvent")]
    public void ReplayFilter_InvalidExpressionIsRejected(string expression)
    {
        Should.Throw<CommandLineSwitchException>(() => CreateOperation("input.binlog", OutputPath(), expression))
            .Message.ShouldContain("MSB1076");
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData(BinaryLogRecordKind.EndOfFile)]
    [InlineData(BinaryLogRecordKind.String)]
    [InlineData(BinaryLogRecordKind.NameValueList)]
    [InlineData(BinaryLogRecordKind.ProjectImportArchive)]
    [InlineData(BinaryLogRecordKind.BuildStarted)]
    [InlineData(BinaryLogRecordKind.BuildFinished)]
    [InlineData(BinaryLogRecordKind.ProjectStarted)]
    [InlineData(BinaryLogRecordKind.ProjectFinished)]
    [InlineData(BinaryLogRecordKind.TargetStarted)]
    [InlineData(BinaryLogRecordKind.TargetFinished)]
    [InlineData(BinaryLogRecordKind.TaskStarted)]
    [InlineData(BinaryLogRecordKind.TaskFinished)]
    [InlineData(BinaryLogRecordKind.TargetSkipped)]
    [InlineData(BinaryLogRecordKind.BuildSubmissionStarted)]
    [InlineData(BinaryLogRecordKind.BuildCanceled)]
    [InlineData(BinaryLogRecordKind.LoggersRegistered)]
    [InlineData(BinaryLogRecordKind.MSBuildServerLifecycle)]
    public void ReplayFilter_ProtectedAndAuxiliaryKindsAreRejected(BinaryLogRecordKind kind)
    {
        Should.Throw<CommandLineSwitchException>(() => CreateOperation("input.binlog", OutputPath(), $"Exclude={kind}"))
            .Message.ShouldContain("MSB1076");
    }

    [Theory]
    [InlineData("ProjectEvaluationStarted")]
    [InlineData("ProjectEvaluationFinished")]
    public void ReplayFilter_UnpairedEvaluationExclusionIsRejected(string kind)
    {
        Should.Throw<CommandLineSwitchException>(() => CreateOperation("input.binlog", OutputPath(), $"Exclude={kind}"))
            .Message.ShouldContain("MSB1083");
    }

    [Fact]
    public void ReplayFilter_PairedEvaluationEventsAreBothRemoved()
    {
        string input = CreateInput();
        string output = OutputPath();
        CreateOperation(input, output, "Exclude=ProjectEvaluationStarted,ProjectEvaluationFinished")
            .Replay([new BinaryLogger()], 1, CancellationToken.None).ShouldBeTrue();

        BuildEventArgs[] events = ReadEvents(output);
        events.ShouldNotContain(e => e is ProjectEvaluationStartedEventArgs || e is ProjectEvaluationFinishedEventArgs);
        events.ShouldContain(e => e.Message == SourceMessage);
        events.OfType<ProjectStartedEventArgs>().ShouldHaveSingleItem();
        events.OfType<ProjectFinishedEventArgs>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("-logger:Missing.Logger,Missing.Assembly")]
    [InlineData("-distributedLogger:Missing.Logger,Missing.Assembly")]
    [InlineData("-fileLogger")]
    [InlineData("-distributedFileLogger")]
    [InlineData("-check")]
    [InlineData("-target:Build")]
    [InlineData("-property:Name=Value")]
    [InlineData("-restore")]
    [InlineData("-getProperty:Name")]
    [InlineData("-getItem:Compile")]
    [InlineData("-getTargetResult:Build")]
    [InlineData("-terminalLogger:true")]
    [InlineData("-nodeMode:1")]
    public void ReplayFilter_IncompatibleSwitchIsRejectedBeforeReplay(string argument)
    {
        string input = _env.CreateFile("input.binlog", "input must not be read or changed").Path;
        byte[] originalInput = File.ReadAllBytes(input);
        string output = OutputPath();

        Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output), "-replayFilter:Exclude=Message", argument)
            .ShouldBe(MSBuildApp.ExitType.SwitchError, diagnostic);

        diagnostic.ShouldContain("MSB1077");
        File.ReadAllBytes(input).ShouldBe(originalInput);
        File.Exists(output).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("-preprocess:")]
    [InlineData("-targets:")]
    [InlineData("-getResultOutputFile:")]
    [InlineData("-fileLoggerParameters:LogFile=")]
    public void ReplayFilter_IncompatibleWriterNeverTruncatesItsDestination(string switchPrefix)
    {
        string input = _env.CreateFile("input.binlog", "unchanged input").Path;
        string otherOutput = _env.CreateFile("existing-output.txt", "keep this output").Path;
        byte[] originalOutput = File.ReadAllBytes(otherOutput);
        string output = OutputPath();

        Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output),
            "-replayFilter:Exclude=Message", $"{switchPrefix}\"{otherOutput}\"")
            .ShouldBe(MSBuildApp.ExitType.SwitchError, diagnostic);

        diagnostic.ShouldContain("MSB1077");
        File.ReadAllBytes(otherOutput).ShouldBe(originalOutput);
        File.Exists(output).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData(".proj")]
    [InlineData(".sln")]
    public void ReplayFilter_ProjectAndSolutionInputsAreRejected(string extension)
    {
        string input = _env.CreateFile($"input{extension}", "<Project />").Path;
        string output = OutputPath();
        Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output), "-replayFilter:Exclude=Message")
            .ShouldBe(MSBuildApp.ExitType.SwitchError, diagnostic);
        diagnostic.ShouldContain("MSB1078");
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public void ReplayFilter_MultipleInputsAreRejected()
    {
        string first = _env.CreateFile("first.binlog", "first").Path;
        string second = _env.CreateFile("second.binlog", "second").Path;
        string output = OutputPath();
        Execute(out string diagnostic, $"\"{first}\"", $"\"{second}\"", BinaryLogArgument(output), "-replayFilter:Exclude=Message")
            .ShouldBe(MSBuildApp.ExitType.SwitchError, diagnostic);
        diagnostic.ShouldContain("MSB1008");
        File.ReadAllText(first).ShouldBe("first");
        File.ReadAllText(second).ShouldBe("second");
        File.Exists(output).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void ReplayFilter_RequiresOneOutputEvenWhenRepeatedPathsAreIdentical(int count, bool duplicate)
    {
        string input = _env.CreateFile("input.binlog", "unchanged input").Path;
        string first = OutputPath();
        string second = OutputPath("second.binlog");
        List<string> arguments = [$"\"{input}\"", "-replayFilter:Exclude=Message"];
        if (count > 0)
        {
            arguments.Add(BinaryLogArgument(first));
            arguments.Add(BinaryLogArgument(duplicate ? first : second));
        }

        Execute(out string diagnostic, arguments.ToArray()).ShouldBe(MSBuildApp.ExitType.SwitchError, diagnostic);
        diagnostic.ShouldContain("MSB1079");
        File.ReadAllText(input).ShouldBe("unchanged input");
        File.Exists(first).ShouldBeFalse();
        File.Exists(second).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("file")]
    [InlineData("directory")]
    [InlineData("input")]
    [InlineData("default")]
    public void ReplayFilter_ExistingDestinationIsNeverModified(string destination)
    {
        string input = CreateInput(destination == "default" ? "msbuild.binlog" : "input.binlog");
        byte[] originalInput = File.ReadAllBytes(input);
        string output = destination is "input" or "default" ? input : OutputPath();
        string sentinel = output;
        if (destination == "directory")
        {
            Directory.CreateDirectory(output);
            sentinel = Path.Combine(output, "sentinel.txt");
        }

        if (destination is "file" or "directory")
        {
            File.WriteAllText(sentinel, "do not overwrite");
        }

        byte[] originalOutput = File.ReadAllBytes(sentinel);
        if (destination == "default")
        {
            _env.SetCurrentDirectory(_env.DefaultTestDirectory.Path);
        }

        Execute(out string diagnostic, $"\"{input}\"", destination == "default" ? "-bl" : BinaryLogArgument(output),
            "-replayFilter:Exclude=Message").ShouldBe(MSBuildApp.ExitType.SwitchError, diagnostic);

        diagnostic.ShouldContain("MSB1080");
        File.ReadAllBytes(input).ShouldBe(originalInput);
        File.ReadAllBytes(sentinel).ShouldBe(originalOutput);
        AssertNoStagingFiles();
    }

    [Fact]
    public void ReplayFilter_ZipFileImportsAreRejected()
    {
        string output = OutputPath();
        Should.Throw<CommandLineSwitchException>(
            () => CreateOperation("input.binlog", output, parameters: ";ProjectImports=ZipFile"))
            .Message.ShouldContain("MSB1084");
        File.Exists(output).ShouldBeFalse();
        File.Exists(Path.ChangeExtension(output, ".ProjectImports.zip")).ShouldBeFalse();
    }

    [Fact]
    public void ReplayFilter_InvalidOutputPathIsRejected()
    {
        Should.Throw<CommandLineSwitchException>(() => CreateOperation("input.binlog", "invalid\0.binlog"))
            .Message.ShouldContain("MSB1085");
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("")]
    [InlineData("-terminalLogger:false")]
    [InlineData("-terminalLogger:auto")]
    [InlineData("-terminalLoggerParameters:default=auto;DISABLENODEDISPLAY")]
    [InlineData("-noConsoleLogger")]
    public void ReplayFilter_BenignLoggingAndExecutionOptionsAreAccepted(string loggerArgument)
    {
        string input = CreateInput();
        string output = OutputPath();
        if (loggerArgument.Length == 0)
        {
            _env.SetEnvironmentVariable("MSBUILDTERMINALLOGGER", "true");
        }

        Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output, ";ProjectImports=None;OmitInitialInfo"),
            "-replayFilter:Exclude=Warning", "-verbosity:diagnostic", "-consoleLoggerParameters:NoSummary",
            "-maxCpuCount:2", "-nodeReuse:false", "-lowPriority:false", loggerArgument)
            .ShouldBe(MSBuildApp.ExitType.Success, diagnostic);

        BuildEventArgs[] events = ReadEvents(output);
        events.ShouldNotContain(e => e is BuildWarningEventArgs);
        events.ShouldContain(e => e.Message == SourceMessage);
        if (loggerArgument != "-noConsoleLogger")
        {
            diagnostic.ShouldContain(SourceMessage);
            diagnostic.ShouldNotContain(SourceWarning);
        }

        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("-help")]
    [InlineData("-version")]
    public void ReplayFilter_HelpAndVersionBypassOperationValidation(string argument)
    {
        Execute(out string diagnostic, "-replayFilter:Exclude=BuildStarted", argument)
            .ShouldBe(MSBuildApp.ExitType.Success, diagnostic);
        diagnostic.ShouldNotContain("MSB1076");
        diagnostic.ShouldNotContain("MSB1079");
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public void ReplayFilter_RecordedBuildFailureDoesNotFailTheTransformation(bool succeeded, bool includeError, bool excludeError)
    {
        string input = CreateInput(succeeded: succeeded, includeError: includeError);
        string output = OutputPath();
        string filter = excludeError ? "-replayFilter:Exclude=Message,Error" : "-replayFilter:Exclude=Message";
        Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output), filter)
            .ShouldBe(MSBuildApp.ExitType.Success, diagnostic);

        BuildEventArgs[] events = ReadEvents(output);
        events.OfType<BuildFinishedEventArgs>().ShouldHaveSingleItem().Succeeded.ShouldBe(succeeded);
        events.OfType<BuildErrorEventArgs>().Count().ShouldBe(includeError && !excludeError ? 1 : 0);
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplayFilter_WildcardPublishesOneFileWithFinalPathMetadata(bool omitInitialInfo)
    {
        string input = CreateInput();
        string pattern = OutputPath("filtered-{}.binlog");
        var logger = new BinaryLogger();
        CreateOperation(input, pattern, parameters: omitInitialInfo ? ";OmitInitialInfo" : "")
            .Replay([logger], 1, CancellationToken.None).ShouldBeTrue();

        string output = Directory.GetFiles(_env.DefaultTestDirectory.Path, "filtered-*.binlog").ShouldHaveSingleItem();
        Path.GetFileName(output).ShouldNotContain("{}");
        BinaryLogger.ParseParameters(logger.Parameters).LogFilePath.ShouldBe(output);
        BuildEventArgs[] events = ReadEvents(output);
        if (omitInitialInfo)
        {
            events.Where(e => e.Message is not null)
                .ShouldNotContain(e => e.Message!.StartsWith("BinLogFilePath=", StringComparison.Ordinal));
        }
        else
        {
            events.ShouldContain(e => e.Message == $"BinLogFilePath={output}");
        }

        events.Where(e => e.Message is not null).ShouldNotContain(e => e.Message!.Contains(".msbuild-filter-"));
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(";ProjectImports=Embed", true)]
    [InlineData(";ProjectImports=None", false)]
    public void ReplayFilter_ImportArchivesAreIndependentOfExcludedImportEvents(string parameters, bool expectArchive)
    {
        string input = CreateInput(embedImports: true);
        string imported = Path.Combine(_env.DefaultTestDirectory.Path, "imported.targets");
        File.Delete(imported);
        string output = OutputPath();

        CreateOperation(input, output, "Exclude=ProjectImported", parameters)
            .Replay([new BinaryLogger()], 1, CancellationToken.None).ShouldBeTrue();

        ReadEvents(output).ShouldNotContain(e => e is ProjectImportedEventArgs);
        List<string> contents = [];
        using (var reader = BinaryLogReplayEventSource.OpenBuildEventsReader(output))
        {
            reader.ArchiveFileEncountered += args => contents.Add(args.ArchiveData.ToArchiveFile().Content);
            while (reader.Read() is not null)
            {
            }
        }

        contents.Contains(ImportedContent).ShouldBe(expectArchive);
        if (!expectArchive)
        {
            contents.ShouldBeEmpty();
        }

        File.Exists(Path.ChangeExtension(output, ".ProjectImports.zip")).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Fact]
    public void ReplayFilter_NewerFormatIsRejectedBeforeCreatingOutputDirectory()
    {
        string seed = CreateInput();
        using var reader = BinaryLogReplayEventSource.OpenReader(seed);
        int version = reader.ReadInt32();
        int minimumReaderVersion = reader.ReadInt32();
        string input = OutputPath("future.binlog");
        WriteCompressedLog(input, writer =>
        {
            writer.Write(version + 1);
            writer.Write(minimumReaderVersion);
            writer.Write((byte)BinaryLogRecordKind.EndOfFile);
        });
        string directory = Path.Combine(_env.DefaultTestDirectory.Path, "not-created");
        string output = Path.Combine(directory, "output.binlog");

        Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output), "-replayFilter:Exclude=Message")
            .ShouldBe(MSBuildApp.ExitType.BuildError, diagnostic);

        diagnostic.ShouldContain("MSB1081");
        Directory.Exists(directory).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplayFilter_UnknownAndCorruptSupportedRecordsFailWithoutPublishing(bool unknownRecord)
    {
        string seed = CreateInput();
        using var reader = BinaryLogReplayEventSource.OpenReader(seed);
        int version = reader.ReadInt32();
        int minimumReaderVersion = reader.ReadInt32();
        string input = OutputPath("corrupt.binlog");
        WriteCompressedLog(input, writer =>
        {
            writer.Write(version);
            writer.Write(minimumReaderVersion);
            writer.Write7BitEncodedInt(unknownRecord ? 127 : (int)BinaryLogRecordKind.BuildStarted);
            writer.Write7BitEncodedInt(unknownRecord ? 1 : 0);
            if (unknownRecord)
            {
                writer.Write((byte)0);
            }

            writer.Write((byte)BinaryLogRecordKind.EndOfFile);
        });
        byte[] originalInput = File.ReadAllBytes(input);
        string output = OutputPath();

        Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output), "-replayFilter:Exclude=Message")
            .ShouldBe(MSBuildApp.ExitType.BuildError, diagnostic);

        diagnostic.ShouldContain("MSB1081");
        File.ReadAllBytes(input).ShouldBe(originalInput);
        File.Exists(output).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Fact]
    public void ReplayFilter_LegacyUnframedLogCanBeRewritten()
    {
        string framed = OutputPath("framed.binlog");
        WriteEvents(framed,
        [
            new BuildMessageEventArgs(null, null, null, MessageImportance.Normal)
            {
                BuildEventContext = new BuildEventContext(1, 2, 3, 4),
            },
        ]);
        string input = OutputPath("legacy.binlog");
        WriteCompressedLog(input, writer =>
        {
            using var reader = BinaryLogReplayEventSource.OpenReader(framed);
            reader.ReadInt32();
            reader.ReadInt32();
            // Version 17 predates record lengths; this message's payload is unchanged.
            writer.Write(17);
            int kind;
            while ((kind = reader.Read7BitEncodedInt()) != (int)BinaryLogRecordKind.EndOfFile)
            {
                int length = reader.Read7BitEncodedInt();
                byte[] payload = reader.ReadBytes(length);
                payload.Length.ShouldBe(length);
                writer.Write7BitEncodedInt(kind);
                writer.Write(payload);
            }

            writer.Write((byte)BinaryLogRecordKind.EndOfFile);
        });
        string output = OutputPath();

        CreateOperation(input, output, "Exclude=Warning", ";OmitInitialInfo")
            .Replay([new BinaryLogger()], 1, CancellationToken.None).ShouldBeTrue();

        BuildMessageEventArgs message = ReadEvents(output).OfType<BuildMessageEventArgs>().ShouldHaveSingleItem();
        message.BuildEventContext!.ProjectContextId.ShouldBe(3);
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("event")]
    [InlineData("shutdown")]
    public void ReplayFilter_LoggerFailuresShutdownAttemptedLoggersAndRemoveStaging(string phase)
    {
        string input = CreateInput();
        byte[] originalInput = File.ReadAllBytes(input);
        string output = OutputPath();
        var preceding = new CallbackLogger();
        var failing = new CallbackLogger
        {
            InitializeAction = phase == "initialize" ? () => throw new InvalidOperationException("initialization failed") : null,
            EventAction = phase == "event" ? _ => throw new InvalidOperationException("event failed") : null,
            ShutdownAction = phase == "shutdown" ? () => throw new InvalidOperationException("shutdown failed") : null,
        };
        var following = new CallbackLogger();

        Capture(
            () => CreateOperation(input, output).Replay([new BinaryLogger(), preceding, failing, following], 1, CancellationToken.None),
            out string diagnostic).ShouldBeFalse();

        diagnostic.ShouldContain("MSB1081");
        preceding.InitializeCount.ShouldBe(1);
        preceding.ShutdownCount.ShouldBe(1);
        failing.InitializeCount.ShouldBe(1);
        failing.ShutdownCount.ShouldBe(1);
        following.InitializeCount.ShouldBe(phase == "initialize" ? 0 : 1);
        following.ShutdownCount.ShouldBe(phase == "initialize" ? 0 : 1);
        File.Exists(output).ShouldBeFalse();
        File.ReadAllBytes(input).ShouldBe(originalInput);
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplayFilter_CancellationNeverPublishesAndShutsDownStartedLoggers(bool cancelBeforeReplay)
    {
        string input = CreateInput();
        string output = OutputPath();
        using var cancellation = new CancellationTokenSource();
        var logger = new CallbackLogger { EventAction = _ => cancellation.Cancel() };
        if (cancelBeforeReplay)
        {
            cancellation.Cancel();
        }

        Capture(
            () => CreateOperation(input, output).Replay([new BinaryLogger(), logger], 1, cancellation.Token),
            out string diagnostic).ShouldBeFalse();

        diagnostic.ShouldContain("MSB1082");
        logger.InitializeCount.ShouldBe(cancelBeforeReplay ? 0 : 1);
        logger.ShutdownCount.ShouldBe(cancelBeforeReplay ? 0 : 1);
        File.Exists(output).ShouldBeFalse();
        AssertNoStagingFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplayFilter_PublicationWaitsForShutdownAndNeverClobbersRacingDestination(bool createRacingDestination)
    {
        string input = CreateInput();
        byte[] originalInput = File.ReadAllBytes(input);
        string output = OutputPath();
        string? staging = null;
        var logger = new CallbackLogger
        {
            ShutdownAction = () =>
            {
                File.Exists(output).ShouldBeFalse();
                staging = Directory.GetFiles(_env.DefaultTestDirectory.Path, ".msbuild-filter-*.binlog").ShouldHaveSingleItem();
                Path.GetDirectoryName(staging).ShouldBe(Path.GetDirectoryName(output));
                Guid.TryParseExact(Path.GetFileNameWithoutExtension(staging).Substring(".msbuild-filter-".Length), "N", out _)
                    .ShouldBeTrue();
                if (createRacingDestination)
                {
                    File.WriteAllText(output, "racing destination");
                }
            },
        };

        bool succeeded = Capture(
            () => CreateOperation(input, output).Replay([new BinaryLogger(), logger], 1, CancellationToken.None),
            out string diagnostic);

        succeeded.ShouldBe(!createRacingDestination, diagnostic);
        logger.ShutdownCount.ShouldBe(1);
        staging.ShouldNotBeNull();
        File.Exists(staging).ShouldBeFalse();
        if (createRacingDestination)
        {
            diagnostic.ShouldContain("MSB1081");
            File.ReadAllText(output).ShouldBe("racing destination");
        }
        else
        {
            ReadEvents(output).OfType<BuildFinishedEventArgs>().ShouldHaveSingleItem();
            using var exclusive = new FileStream(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        File.ReadAllBytes(input).ShouldBe(originalInput);
        AssertNoStagingFiles();
    }

    [Fact]
    public void ReplayFilter_BinaryLoggerRequiresAnOutputStream()
    {
        string path = OutputPath();
        var logger = new BinaryLogger { Parameters = $"LogFile={path}" };

        Should.Throw<ArgumentNullException>(() => logger.Initialize(new EventArgsDispatcher(), null!))
            .ParamName.ShouldBe("outputStream");
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void ReplayFilter_BinaryLoggerOwnsTheSuppliedStreamWithoutCreatingTheLogFile()
    {
        string path = OutputPath();
        using var output = new MemoryStream();
        var events = new EventArgsDispatcher();
        var logger = new BinaryLogger { Parameters = $"LogFile={path};ProjectImports=None;OmitInitialInfo" };
        try
        {
            logger.Initialize(events, output);
            events.Dispatch(new BuildStartedEventArgs("started", string.Empty));
            events.Dispatch(new BuildFinishedEventArgs("finished", string.Empty, succeeded: true));
        }
        finally
        {
            logger.Shutdown();
        }

        output.CanWrite.ShouldBeFalse();
        File.Exists(path).ShouldBeFalse();
        List<BuildEventArgs> replayed = [];
        var source = new BinaryLogReplayEventSource();
        source.AnyEventRaised += (_, e) => replayed.Add(e);
        using var input = new MemoryStream(output.ToArray());
        source.Replay(input, CancellationToken.None);
        replayed.OfType<BuildStartedEventArgs>().ShouldHaveSingleItem();
        replayed.OfType<BuildFinishedEventArgs>().ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplayFilter_OnlyTheCurrentSdkLoggerCanBeIgnored(bool currentSdk)
    {
        string input = CreateInput();
        string output = OutputPath();
        string directory = currentSdk ? BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory : _env.DefaultTestDirectory.Path;
        string assemblyPath = Path.Combine(directory, "dotnet.dll");
        string logger = $"-distributedlogger:Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildLogger,{assemblyPath}"
            + $"*Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildForwardingLogger,{assemblyPath}";

        var result = Execute(out string diagnostic, $"\"{input}\"", BinaryLogArgument(output),
            "-replayFilter:Exclude=Warning", logger, "-tlp:default=auto", "-tlp:DISABLENODEDISPLAY");

        if (currentSdk)
        {
            result.ShouldBe(MSBuildApp.ExitType.Success, diagnostic);
            ReadEvents(output).ShouldNotContain(e => e is BuildWarningEventArgs);
        }
        else
        {
            result.ShouldBe(MSBuildApp.ExitType.SwitchError, diagnostic);
            diagnostic.ShouldContain("MSB1077");
            File.Exists(output).ShouldBeFalse();
        }

        AssertNoStagingFiles();
    }

    public void Dispose() => _env.Dispose();

    private string OutputPath(string name = "filtered.binlog") => Path.Combine(_env.DefaultTestDirectory.Path, name);

    private static string BinaryLogArgument(string output, string parameters = "") => $"-bl:\"{output}{parameters}\"";

    private static CommandLineSwitches ParseSwitches(params string[] arguments)
    {
        var parser = new CommandLineParser();
        parser.ResetGatheringSwitchesState();
        var switches = new CommandLineSwitches();
        parser.GatherCommandLineSwitches(arguments, switches);
        switches.ThrowErrors();
        return switches;
    }

    private static FilteredBinlogReplay CreateOperation(
        string input, string output, string expression = "Exclude=Message", string parameters = "")
        => FilteredBinlogReplay.Create(input, ParseSwitches($"-replayFilter:\"{expression}\"", BinaryLogArgument(output, parameters)));

    private static MSBuildApp.ExitType Execute(out string diagnostic, params string[] arguments)
        => Capture(() => MSBuildApp.Execute(["msbuild.exe", "-noAutoResponse", "-nologo", .. arguments]), out diagnostic);

    private static T Capture<T>(Func<T> action, out string diagnostic)
    {
        TextWriter previous = Console.Out;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(writer);
            return action();
        }
        finally
        {
            Console.SetOut(previous);
            diagnostic = writer.ToString();
        }
    }

    private string CreateInput(string name = "input.binlog", bool succeeded = true, bool includeError = false, bool embedImports = false)
    {
        string input = OutputPath(name);
        string project = _env.CreateFile("source.proj", "<Project />").Path;
        var context = new BuildEventContext(1, 1, 1, 1, 101, 11, 111);
        List<BuildEventArgs> events =
        [
            new BuildStartedEventArgs("source build started", string.Empty),
            new ProjectEvaluationStartedEventArgs("source evaluation started") { ProjectFile = project },
            new ProjectEvaluationFinishedEventArgs("source evaluation finished") { ProjectFile = project },
            new ProjectStartedEventArgs(1, "source project started", "", project, "Build", null, null, BuildEventContext.Invalid),
            new TargetStartedEventArgs("source target started", "", "Build", project, project),
            new TaskStartedEventArgs("source task started", "", project, project, "Message"),
            new BuildMessageEventArgs(SourceMessage, null, "Message", MessageImportance.High),
            new BuildWarningEventArgs("", "RF0001", project, 1, 1, 1, 1, SourceWarning, "", "Message"),
            new CriticalBuildMessageEventArgs("", "", project, 1, 1, 1, 1, "source critical message", "", "Message"),
            new TaskCommandLineEventArgs("source tool command line", "Message", MessageImportance.High),
        ];
        if (embedImports)
        {
            string imported = _env.CreateFile("imported.targets", ImportedContent).Path;
            events.Add(new ProjectImportedEventArgs(1, 1, "source project imported")
            {
                ImportedProjectFile = imported,
                UnexpandedProject = imported,
            });
        }

        if (includeError)
        {
            events.Add(new BuildErrorEventArgs("", "RF0002", project, 1, 1, 1, 1, SourceError, "", "Message"));
        }

        events.AddRange(
        [
            new TaskFinishedEventArgs("source task finished", "", project, project, "Message", succeeded),
            new TargetFinishedEventArgs("source target finished", "", "Build", project, project, succeeded),
            new TargetSkippedEventArgs("source target skipped")
            {
                OriginalBuildEventContext = context,
                TargetName = "Skipped",
                ProjectFile = project,
                TargetFile = project,
                BuildReason = TargetBuiltReason.DependsOn,
                SkipReason = TargetSkipReason.PreviouslyBuiltSuccessfully,
                OriginallySucceeded = true,
            },
            new ProjectFinishedEventArgs("source project finished", null, project, succeeded),
            new BuildFinishedEventArgs("source build finished", null, succeeded),
        ]);
        foreach (BuildEventArgs e in events)
        {
            e.BuildEventContext = context;
        }

        WriteEvents(input, events, embedImports);
        return input;
    }

    private static void WriteEvents(string path, IEnumerable<BuildEventArgs> events, bool embedImports = false)
    {
        var source = new EventArgsDispatcher();
        var logger = new BinaryLogger
        {
            Parameters = $"LogFile={path};OmitInitialInfo;ProjectImports={(embedImports ? "Embed" : "None")}",
        };
        try
        {
            logger.Initialize(source);
            foreach (BuildEventArgs e in events)
            {
                source.Dispatch(e);
            }
        }
        finally
        {
            logger.Shutdown();
        }
    }

    private static BuildEventArgs[] ReadEvents(string path)
    {
        List<BuildEventArgs> events = [];
        var source = new BinaryLogReplayEventSource();
        source.AnyEventRaised += (_, e) => events.Add(e);
        source.Replay(path);
        return events.ToArray();
    }

    private static void WriteCompressedLog(string path, Action<BinaryWriter> write)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        using var writer = new BinaryWriter(gzip, Encoding.UTF8);
        write(writer);
    }

    private void AssertNoStagingFiles()
        => Directory.GetFiles(_env.DefaultTestDirectory.Path, ".msbuild-filter-*.binlog", SearchOption.AllDirectories).ShouldBeEmpty();

    private sealed class CallbackLogger : ILogger
    {
        private IEventSource? _source;

        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
        public string? Parameters { get; set; }
        public Action? InitializeAction { get; init; }
        public Action<BuildEventArgs>? EventAction { get; init; }
        public Action? ShutdownAction { get; init; }
        public List<BuildEventArgs> Events { get; } = [];
        public int InitializeCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void Initialize(IEventSource eventSource)
        {
            InitializeCount++;
            _source = eventSource;
            eventSource.AnyEventRaised += OnEvent;
            InitializeAction?.Invoke();
        }

        public void Shutdown()
        {
            ShutdownCount++;
            if (_source is not null)
            {
                _source.AnyEventRaised -= OnEvent;
            }

            ShutdownAction?.Invoke();
        }

        private void OnEvent(object sender, BuildEventArgs e)
        {
            Events.Add(e);
            EventAction?.Invoke(e);
        }
    }
}
