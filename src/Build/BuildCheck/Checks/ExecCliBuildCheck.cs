// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using Microsoft.Build.Shared;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Experimental.BuildCheck.Checks;

internal sealed class ExecCliBuildCheck : Check
{
    public static CheckRule SupportedRule = new CheckRule(
        "BC0109",
        "ExecCliBuild",
        ResourceUtilities.GetResourceString("BuildCheck_BC0109_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0109_MessageFmt")!,
        new CheckConfiguration() { Severity = CheckResultSeverity.Warning });

    private const string ExecTaskName = "Exec";
    private const string CommandParameterName = "Command";

    private static readonly char[] s_knownCommandSeparators = ['&', ';', '|'];

    private static readonly KnownBuildCommand[] s_knownBuildCommands =
    [
        new KnownBuildCommand("dotnet build"),
        new KnownBuildCommand("dotnet clean"),
        new KnownBuildCommand("dotnet msbuild"),
        new KnownBuildCommand("dotnet restore"),
        new KnownBuildCommand("dotnet publish"),
        new KnownBuildCommand("dotnet pack"),
        new KnownBuildCommand("dotnet vstest"),
        new KnownBuildCommand("nuget restore"),
        new KnownBuildCommand("msbuild", excludedSwitches: ["version", "ver", "help", "h", "?"]),
        new KnownBuildCommand("dotnet test"),
        new KnownBuildCommand("dotnet run"),
    ];

    public override string FriendlyName => "MSBuild.ExecCliBuildCheck";

    internal override bool IsBuiltIn => true;

    public override IReadOnlyList<CheckRule> SupportedRules { get; } = [SupportedRule];

    public override void Initialize(ConfigurationContext configurationContext)
    {
        /* This is it - no custom configuration */
    }

    public override void RegisterActions(IBuildCheckRegistrationContext registrationContext)
    {
        registrationContext.RegisterTaskInvocationAction(TaskInvocationAction);
    }

    private static void TaskInvocationAction(BuildCheckDataContext<TaskInvocationCheckData> context)
    {
        if (context.Data.TaskName == ExecTaskName
            && context.Data.Parameters.TryGetValue(CommandParameterName, out TaskInvocationCheckData.TaskParameter? commandArgument))
        {
            var execCommandValue = commandArgument.Value?.ToString() ?? string.Empty;

            var commandSpan = execCommandValue.AsSpan();
            int start = 0;

            while (start < commandSpan.Length)
            {
                var nextSeparatorIndex = commandSpan.Slice(start, commandSpan.Length - start).IndexOfAny(s_knownCommandSeparators);

                if (nextSeparatorIndex == -1)
                {
                    if (TryGetMatchingKnownBuildCommand(commandSpan, out var knownBuildCommand))
                    {
                        context.ReportResult(BuildCheckResult.CreateBuiltIn(
                            SupportedRule,
                            context.Data.TaskInvocationLocation,
                            context.Data.TaskName,
                            Path.GetFileName(context.Data.ProjectFilePath),
                            knownBuildCommand.ToolName));
                    }

                    break;
                }
                else
                {
                    var command = commandSpan.Slice(start, nextSeparatorIndex);

                    if (TryGetMatchingKnownBuildCommand(command, out var knownBuildCommand))
                    {
                        context.ReportResult(BuildCheckResult.CreateBuiltIn(
                            SupportedRule,
                            context.Data.TaskInvocationLocation,
                            context.Data.TaskName,
                            Path.GetFileName(context.Data.ProjectFilePath),
                            knownBuildCommand.ToolName));

                        break;
                    }

                    start += nextSeparatorIndex + 1;
                }
            }
        }
    }

    private static bool TryGetMatchingKnownBuildCommand(ReadOnlySpan<char> command, out KnownBuildCommand knownBuildCommand)
    {
        Span<char> normalizedCommand = stackalloc char[command.Length];
        int normalizedCommandIndex = 0;

        foreach (var c in command)
        {
            if (char.IsWhiteSpace(c) && (normalizedCommandIndex == 0 || char.IsWhiteSpace(normalizedCommand[normalizedCommandIndex - 1])))
            {
                continue;
            }

            normalizedCommand[normalizedCommandIndex++] = c;
        }

        foreach (var buildCommand in s_knownBuildCommands)
        {
            if (buildCommand.IsMatch(normalizedCommand))
            {
                knownBuildCommand = buildCommand;
                return true;
            }
        }

        knownBuildCommand = default;
        return false;
    }

    private readonly record struct KnownBuildCommand
    {
        private static readonly string[] s_knownSwitchPrefixes = ["/", "--", "-"];

        private readonly string _knownBuildCommand;
        private readonly string[] _excludedSwitches = [];

        public KnownBuildCommand(string knownBuildCommand)
        {
            if (string.IsNullOrEmpty(knownBuildCommand))
            {
                throw new ArgumentNullException(nameof(knownBuildCommand));
            }

            _knownBuildCommand = knownBuildCommand;
        }

        public KnownBuildCommand(string knownBuildCommand, string[] excludedSwitches)
            : this(knownBuildCommand)
        {
            _excludedSwitches = excludedSwitches;
        }

        public string ToolName
        {
            get
            {
                int nextSpaceIndex = _knownBuildCommand.IndexOf(' ');

                return nextSpaceIndex == -1
                    ? _knownBuildCommand
                    : _knownBuildCommand.AsSpan().Slice(0, nextSpaceIndex).ToString();
            }
        }

        public bool IsMatch(ReadOnlySpan<char> execCommand)
        {
            if (!execCommand.StartsWith(_knownBuildCommand.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_excludedSwitches.Length == 0 || execCommand.Length == _knownBuildCommand.Length)
            {
                return true;
            }

            return !ContainsExcludedArguments(execCommand);
        }

        private bool ContainsExcludedArguments(ReadOnlySpan<char> execCommand)
        {
            int start = _knownBuildCommand.Length + 1;

            while (start < execCommand.Length)
            {
                int nextSpaceIndex = execCommand.Slice(start).IndexOf(' ');

                if (nextSpaceIndex == -1)
                {
                    var argument = execCommand.Slice(start);

                    if (EqualsToAnyExcludedArguments(argument))
                    {
                        return true;
                    }

                    break;
                }
                else
                {
                    var argument = execCommand.Slice(start, nextSpaceIndex);

                    if (EqualsToAnyExcludedArguments(argument))
                    {
                        return true;
                    }

                    start += nextSpaceIndex + 1;
                }
            }

            return false;
        }

        private bool EqualsToAnyExcludedArguments(ReadOnlySpan<char> argument)
        {
            foreach (var knownSwitch in s_knownSwitchPrefixes)
            {
                if (argument.StartsWith(knownSwitch.AsSpan()))
                {
                    foreach (var excludedSwitch in _excludedSwitches)
                    {
                        if (argument.EndsWith(excludedSwitch.AsSpan(), StringComparison.OrdinalIgnoreCase)
                            && argument.Length == knownSwitch.Length + excludedSwitch.Length)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
