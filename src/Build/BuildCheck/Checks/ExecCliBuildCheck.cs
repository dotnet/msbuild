// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using System.Linq;
using System.Text.RegularExpressions;
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
            var execCommands = (commandArgument.EnumerateStringValues().FirstOrDefault() ?? string.Empty)
                .Split(s_knownCommandSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => Regex.Replace(c, @"\s+", " "));

            foreach (var execCommand in execCommands)
            {
                var buildCommand = s_knownBuildCommands.FirstOrDefault(c => c.IsMatch(execCommand));

                if (!buildCommand.Equals(default))
                {
                    context.ReportResult(BuildCheckResult.CreateBuiltIn(
                        SupportedRule,
                        context.Data.TaskInvocationLocation,
                        context.Data.TaskName,
                        Path.GetFileName(context.Data.ProjectFilePath),
                        buildCommand.ToolName));

                    break;
                }
            }
        }
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

        public string ToolName => _knownBuildCommand.Split(' ').FirstOrDefault()!;

        public bool IsMatch(string execCommand)
        {
            if (!execCommand.StartsWith(_knownBuildCommand, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var execCommandArguments = execCommand.Split(' ').Skip(1);

            if (_excludedSwitches.Length == 0 || !execCommandArguments.Any())
            {
                return true;
            }

            var excludedSwitches = _excludedSwitches.SelectMany(excludedSwitch =>
                s_knownSwitchPrefixes.Select(knownSwitchPrefix => $"{knownSwitchPrefix}{excludedSwitch}"));

            return execCommandArguments
                .All(argument => !excludedSwitches.Contains(argument, StringComparer.OrdinalIgnoreCase));
        }
    }
}
