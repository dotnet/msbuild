// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.CommandLine;
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

    public static CommandLineOptions? Parse(string[] args, IReporter reporter, out int errorCode, TextWriter? output = null, TextWriter? error = null)
    {
        var quietOption = new CliOption<bool>("--quiet", "-q")
        {
            Description = "Suppresses all output except warnings and errors"
        };

        var verboseOption = new CliOption<bool>("--verbose", "-v")
        {
            Description = "Show verbose output"
        };

        verboseOption.Validators.Add(v =>
        {
            if (v.GetResult(quietOption) is not null && v.GetResult(verboseOption) is not null)
            {
                v.AddError(Resources.Error_QuietAndVerboseSpecified);
            }
        });

        var listOption = new CliOption<bool>("--list") { Description = "Lists all discovered files without starting the watcher." };
        var shortProjectOption = new CliOption<string>("-p") { Description = "The project to watch.", Hidden = true };
        var longProjectOption = new CliOption<string>("--project") { Description = "The project to watch" };

        // launch profile used by dotnet-watch
        var launchProfileWatchOption = new CliOption<string>(LaunchProfileOptionName, "-lp")
        {
            Description = "The launch profile to start the project with (case-sensitive)."
        };
        var noLaunchProfileWatchOption = new CliOption<bool>(NoLaunchProfileOptionName)
        {
            Description = "Do not attempt to use launchSettings.json to configure the application."
        };

        // launch profile used by dotnet-run
        var launchProfileRunOption = new CliOption<string>(LaunchProfileOptionName, "-lp") { Hidden = true };
        var noLaunchProfileRunOption = new CliOption<bool>(NoLaunchProfileOptionName) { Hidden = true };

        var targetFrameworkOption = new CliOption<string>("--framework", "-f")
        {
            Description = "The target framework to run for. The target framework must also be specified in the project file."
        };
        var propertyOption = new CliOption<string[]>("--property")
        {
            Description = "Properties to be passed to MSBuild."
        };

        propertyOption.Validators.Add(v =>
        {
            var invalidProperty = v.GetValue(propertyOption)?.FirstOrDefault(
                property => !(property.IndexOf('=') is > 0 and var index && index < property.Length - 1 && property[..index].Trim().Length > 0));

            if (invalidProperty != null)
            {
                v.AddError($"Invalid property format: '{invalidProperty}'. Expected 'name=value'.");
            }
        });

        var noHotReloadOption = new CliOption<bool>("--no-hot-reload") { Description = "Suppress hot reload for supported apps." };
        var nonInteractiveOption = new CliOption<bool>("--non-interactive")
        {
            Description = "Runs dotnet-watch in non-interactive mode. This option is only supported when running with Hot Reload enabled. " +
            "Use this option to prevent console input from being captured."
        };

        var remainingWatchArgs = new CliArgument<string[]>("forwardedArgs") { Description = "Arguments to pass to the child dotnet process." };
        var remainingRunArgs = new CliArgument<string[]>("remainingRunArgs");

        var runCommand = new CliCommand("run") { Hidden = true };
        var rootCommand = new CliRootCommand(Description);
        AddSymbols(runCommand);
        AddSymbols(rootCommand);

        void AddSymbols(CliCommand command)
        {
            command.Options.Add(quietOption);
            command.Options.Add(verboseOption);
            command.Options.Add(noHotReloadOption);
            command.Options.Add(nonInteractiveOption);
            command.Options.Add(longProjectOption);
            command.Options.Add(shortProjectOption);

            if (command == runCommand)
            {
                command.Options.Add(launchProfileRunOption);
                command.Options.Add(noLaunchProfileRunOption);
            }
            else
            {
                command.Options.Add(launchProfileWatchOption);
                command.Options.Add(noLaunchProfileWatchOption);
            }

            command.Options.Add(targetFrameworkOption);
            command.Options.Add(propertyOption);

            command.Options.Add(listOption);

            if (command == runCommand)
            {
                command.Arguments.Add(remainingRunArgs);
            }
            else
            {
                command.Subcommands.Add(runCommand);
                command.Arguments.Add(remainingWatchArgs);
            }
        };

        CommandLineOptions? options = null;

        runCommand.SetAction(parseResult =>
        {
            RootHandler(parseResult, new()
            {
                LaunchProfileName = parseResult.GetValue(launchProfileRunOption),
                NoLaunchProfile = parseResult.GetValue(noLaunchProfileRunOption),
                RemainingArguments = parseResult.GetValue(remainingRunArgs) ?? Array.Empty<string>(),
            });
        });

        rootCommand.SetAction(parseResult => RootHandler(parseResult, runOptions: null));

        void RootHandler(ParseResult parseResults, RunCommandLineOptions? runOptions)
        {
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
                RemainingArguments = parseResults.GetValue(remainingWatchArgs) ?? Array.Empty<string>(),
                RunOptions = runOptions,
            };
        }

        errorCode = new CliConfiguration(rootCommand)
        {
            Output = output ?? Console.Out,
            Error = error ?? Console.Error
        }.Invoke(args);

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
