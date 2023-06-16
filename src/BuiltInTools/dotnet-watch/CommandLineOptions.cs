// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using Microsoft.AspNetCore.Authentication;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

internal sealed class RunCommandLineOptions
{
    public required bool NoLaunchProfile { get; init; }
    public required string? LaunchProfileName { get; init; }
    public required IReadOnlyList<string> RemainingArguments { get; init; }
}

internal sealed class CommandLineOptions
{
    private const string Description = @"
Environment variables:

  DOTNET_USE_POLLING_FILE_WATCHER
  When set to '1' or 'true', dotnet-watch will poll the file system for
  changes. This is required for some file systems, such as network shares,
  Docker mounted volumes, and other virtual file systems.

  DOTNET_WATCH
  dotnet-watch sets this variable to '1' on all child processes launched.

  DOTNET_WATCH_ITERATION
  dotnet-watch sets this variable to '1' and increments by one each time
  a file is changed and the command is restarted.

  DOTNET_WATCH_SUPPRESS_EMOJIS
  When set to '1' or 'true', dotnet-watch will not show emojis in the 
  console output.

Remarks:
  The special option '--' is used to delimit the end of the options and
  the beginning of arguments that will be passed to the child dotnet process.

  For example: dotnet watch -- --verbose run

  Even though '--verbose' is an option dotnet-watch supports, the use of '--'
  indicates that '--verbose' should be treated instead as an argument for
  dotnet-run.

Examples:
  dotnet watch run
  dotnet watch test
";

    private const string NoLaunchProfileOptionName = "--no-launch-profile";
    private const string LaunchProfileOptionName = "--launch-profile";
    private const string TargetFrameworkOptionName = "--framework";
    private const string BuildPropertyOptionName = "--property";

    public string? Project { get; init; }
    public string? LaunchProfileName { get; init; }
    public string? TargetFramework { get; init; }
    public IReadOnlyList<(string name, string value)>? BuildProperties { get; init; }
    public bool BinaryLogger { get; init; }
    public bool NoLaunchProfile { get; init; }
    public bool Quiet { get; init; }
    public bool Verbose { get; init; }
    public bool List { get; init; }
    public bool NoHotReload { get; init; }
    public bool NonInteractive { get; init; }
    public required IReadOnlyList<string> RemainingArguments { get; init; }
    public RunCommandLineOptions? RunOptions { get; init; }

    public static CommandLineOptions? Parse(string[] args, IReporter reporter, out int errorCode, System.CommandLine.IConsole? console = null)
    {
        var quietOption = new Option<bool>(new[] { "--quiet", "-q" }, "Suppresses all output except warnings and errors");
        var verboseOption = new Option<bool>(new[] { "--verbose", "-v" }, "Show verbose output");

        verboseOption.AddValidator(v =>
        {
            if (v.FindResultFor(quietOption) is not null && v.FindResultFor(verboseOption) is not null)
            {
                v.ErrorMessage = Resources.Error_QuietAndVerboseSpecified;
            }
        });

        var listOption = new Option<bool>("--list", "Lists all discovered files without starting the watcher.");
        var shortProjectOption = new Option<string>("-p", "The project to watch.") { IsHidden = true };
        var longProjectOption = new Option<string>("--project", "The project to watch");

        // launch profile used by dotnet-watch
        var launchProfileWatchOption = new Option<string>(new[] { "-lp", LaunchProfileOptionName }, "The launch profile to start the project with (case-sensitive).");
        var noLaunchProfileWatchOption = new Option<bool>(new[] { NoLaunchProfileOptionName }, "Do not attempt to use launchSettings.json to configure the application.");

        // launch profile used by dotnet-run
        var launchProfileRunOption = new Option<string>(new[] { "-lp", LaunchProfileOptionName }) { IsHidden = true };
        var noLaunchProfileRunOption = new Option<bool>(new[] { NoLaunchProfileOptionName }) { IsHidden = true };

        var targetFrameworkOption = new Option<string>(new[] { "-f", "--framework" }, "The target framework to run for. The target framework must also be specified in the project file.");
        var propertyOption = new Option<string[]>(new[] { "--property" }, "Properties to be passed to MSBuild.");

        propertyOption.AddValidator(v =>
        {
            var invalidProperty = v.GetValue(propertyOption)?.FirstOrDefault(
                property => !(property.IndexOf('=') is > 0 and var index && index < property.Length - 1 && property[..index].Trim().Length > 0));

            if (invalidProperty != null)
            {
                v.ErrorMessage = $"Invalid property format: '{invalidProperty}'. Expected 'name=value'.";
            }
        });

        var noHotReloadOption = new Option<bool>("--no-hot-reload", "Suppress hot reload for supported apps.");
        var nonInteractiveOption = new Option<bool>(
            "--non-interactive",
            "Runs dotnet-watch in non-interactive mode. This option is only supported when running with Hot Reload enabled. " +
            "Use this option to prevent console input from being captured.");

        var remainingWatchArgs = new Argument<string[]>("forwardedArgs", "Arguments to pass to the child dotnet process.");
        var remainingRunArgs = new Argument<string[]>(name: null);

        var runCommand = new Command("run") { IsHidden = true };
        var rootCommand = new RootCommand(Description);
        addOptions(runCommand);
        addOptions(rootCommand);

        void addOptions(Command command)
        {
            command.Add(quietOption);
            command.Add(verboseOption);
            command.Add(noHotReloadOption);
            command.Add(nonInteractiveOption);
            command.Add(longProjectOption);
            command.Add(shortProjectOption);

            if (command == runCommand)
            {
                command.Add(launchProfileRunOption);
                command.Add(noLaunchProfileRunOption);
            }
            else
            {
                command.Add(launchProfileWatchOption);
                command.Add(noLaunchProfileWatchOption);
            }

            command.Add(targetFrameworkOption);
            command.Add(propertyOption);

            command.Add(listOption);

            if (command == runCommand)
            {
                command.Add(remainingRunArgs);
            }
            else
            {
                command.Add(runCommand);
                command.Add(remainingWatchArgs);
            }
        };

        CommandLineOptions? options = null;

        runCommand.SetHandler(context =>
        {
            RootHandler(context, new()
            {
                LaunchProfileName = context.ParseResult.GetValue(launchProfileRunOption),
                NoLaunchProfile = context.ParseResult.GetValue(noLaunchProfileRunOption),
                RemainingArguments = context.ParseResult.GetValue(remainingRunArgs),
            });
        });

        rootCommand.SetHandler(context => RootHandler(context, runOptions: null));

        void RootHandler(InvocationContext context, RunCommandLineOptions? runOptions)
        {
            var parseResults = context.ParseResult;
            var projectValue = parseResults.GetValue(longProjectOption);
            if (string.IsNullOrEmpty(projectValue))
            {
                var projectShortValue = parseResults.GetValue(shortProjectOption);
                if (!string.IsNullOrEmpty(projectShortValue))
                {
                    reporter.Warn(Resources.Warning_ProjectAbbreviationDeprecated);
                    projectValue = projectShortValue;
                }
            }

            options = new()
            {
                Quiet = parseResults.GetValue(quietOption),
                List = parseResults.GetValue(listOption),
                NoHotReload = parseResults.GetValue(noHotReloadOption),
                NonInteractive = parseResults.GetValue(nonInteractiveOption),
                Verbose = parseResults.GetValue(verboseOption),
                Project = projectValue,
                LaunchProfileName = parseResults.GetValue(launchProfileWatchOption),
                NoLaunchProfile = parseResults.GetValue(noLaunchProfileWatchOption),
                TargetFramework = parseResults.GetValue(targetFrameworkOption),
                BuildProperties = parseResults.GetValue(propertyOption)?
                    .Select(p => (p[..p.IndexOf('=')].Trim(), p[(p.IndexOf('=') + 1)..])).ToArray(),
                RemainingArguments = parseResults.GetValue(remainingWatchArgs),
                RunOptions = runOptions,
            };
        }

        errorCode = rootCommand.Invoke(args, console);
        return options;
    }

    public IReadOnlyList<string> GetLaunchProcessArguments(bool hotReload, IReporter reporter, out bool watchNoLaunchProfile, out string? watchLaunchProfileName)
    {
        var argsBuilder = new List<string>();
        if (!hotReload)
        {
            // Arguments are passed to dotnet and the first argument is interpreted as a command.
            argsBuilder.Add("run");

            // add options that are applicable to dotnet run:

            if (TargetFramework != null)
            {
                argsBuilder.Add(TargetFrameworkOptionName);
                argsBuilder.Add(TargetFramework);
            }

            if (BuildProperties != null)
            {
                foreach (var (name, value) in BuildProperties)
                {
                    argsBuilder.Add(BuildPropertyOptionName);
                    argsBuilder.Add($"{name}={value}");
                }
            }
        }

        argsBuilder.AddRange(RemainingArguments);

        // launch profile:
        if (hotReload)
        {
            watchNoLaunchProfile = NoLaunchProfile || RunOptions?.NoLaunchProfile == true;
            watchLaunchProfileName = LaunchProfileName ?? RunOptions?.LaunchProfileName;

            if (LaunchProfileName != null && RunOptions?.LaunchProfileName != null)
            {
                reporter.Warn($"Using launch profile name '{LaunchProfileName}', ignoring '{RunOptions.LaunchProfileName}'.");
            }
        }
        else
        {
            var runNoLaunchProfile = (RunOptions != null) ? RunOptions.NoLaunchProfile : NoLaunchProfile;
            watchNoLaunchProfile = NoLaunchProfile;

            var runLaunchProfileName = (RunOptions != null) ? RunOptions.LaunchProfileName : LaunchProfileName;
            watchLaunchProfileName = LaunchProfileName;

            if (runNoLaunchProfile)
            {
                argsBuilder.Add(NoLaunchProfileOptionName);
            }

            if (runLaunchProfileName != null)
            {
                argsBuilder.Add(LaunchProfileOptionName);
                argsBuilder.Add(runLaunchProfileName);
            }
        }

        if (RunOptions != null)
        {
            argsBuilder.AddRange(RunOptions.RemainingArguments);
        }

        return argsBuilder.ToArray();
    }
}
