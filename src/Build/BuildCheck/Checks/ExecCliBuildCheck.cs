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
        "BC0302",
        "ExecCliBuild",
        ResourceUtilities.GetResourceString("BuildCheck_BC0302_Title")!,
        ResourceUtilities.GetResourceString("BuildCheck_BC0302_MessageFmt")!,
        new CheckConfiguration() { Severity = CheckResultSeverity.Warning });

    private const string ExecTaskName = "Exec";
    private const string CommandParameterName = "Command";

    private static readonly char[] s_knownCommandSeparators = ['&', ';', '|'];

    private static readonly string[] s_knownBuildCommands =
    [
        "dotnet build",
        "dotnet clean",
        "dotnet msbuild",
        "dotnet restore",
        "dotnet publish",
        "dotnet pack",
        "dotnet vstest",
        "nuget restore",
        "msbuild",
        "dotnet test",
        "dotnet run",
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
                    if (TryGetMatchingKnownBuildCommand(commandSpan.Slice(start), out var knownBuildCommand))
                    {
                        context.ReportResult(BuildCheckResult.CreateBuiltIn(
                            SupportedRule,
                            context.Data.TaskInvocationLocation,
                            context.Data.TaskName,
                            Path.GetFileName(context.Data.ProjectFilePath),
                            GetToolName(knownBuildCommand)));
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
                            GetToolName(knownBuildCommand)));

                        break;
                    }

                    start += nextSeparatorIndex + 1;
                }
            }
        }
    }

    private static bool TryGetMatchingKnownBuildCommand(ReadOnlySpan<char> command, out string knownBuildCommand)
    {
        const int maxStackLimit = 1024;

        Span<char> normalizedBuildCommand = command.Length <= maxStackLimit ? stackalloc char[command.Length] : new char[command.Length];
        int normalizedCommandIndex = 0;

        foreach (var c in command)
        {
            if (char.IsWhiteSpace(c) && (normalizedCommandIndex == 0 || char.IsWhiteSpace(normalizedBuildCommand[normalizedCommandIndex - 1])))
            {
                continue;
            }

            normalizedBuildCommand[normalizedCommandIndex++] = c;
        }

        foreach (var buildCommand in s_knownBuildCommands)
        {
            if (normalizedBuildCommand.StartsWith(buildCommand.AsSpan()))
            {
                knownBuildCommand = buildCommand;
                return true;
            }
        }

        knownBuildCommand = string.Empty;
        return false;
    }

    private static string GetToolName(string knownBuildCommand)
    {
        int nextSpaceIndex = knownBuildCommand.IndexOf(' ');

        return nextSpaceIndex == -1
            ? knownBuildCommand
            : knownBuildCommand.AsSpan().Slice(0, nextSpaceIndex).ToString();
    }
}
