// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Run.LaunchSettings;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public string Configuration { get; private set; }
        public string Framework { get; private set; }
        public string Runtime { get; private set; }
        public bool NoBuild { get; private set; }
        public string Project { get; private set; }
        public IEnumerable<string> Args { get; set; }
        public bool NoRestore { get; private set; }
        public bool Interactive { get; private set; }
        public IEnumerable<string> RestoreArgs { get; private set; }

        private Version Version6_0 = new Version(6, 0);
        private bool ShouldBuild => !NoBuild;
        private bool HasQuietVerbosity =>
            RestoreArgs.All(arg => !arg.StartsWith("-verbosity:", StringComparison.Ordinal) ||
                                    arg.Equals("-verbosity:q", StringComparison.Ordinal) ||
                                    arg.Equals("-verbosity:quiet", StringComparison.Ordinal));

        public string LaunchProfile { get; private set; }
        public bool NoLaunchProfile { get; private set; }
        private bool UseLaunchProfile => !NoLaunchProfile;

        public int Execute()
        {
            Initialize();

            if (!TryGetLaunchProfileSettingsIfNeeded(out var launchSettings))
            {
                return 1;
            }

            if (ShouldBuild)
            {
                if (string.Equals("true", launchSettings?.DotNetRunMessages, StringComparison.OrdinalIgnoreCase))
                {
                    Reporter.Output.WriteLine(LocalizableStrings.RunCommandBuilding);
                }

                EnsureProjectIsBuilt();
            }

            try
            {
                ICommand targetCommand = GetTargetCommand();
                if (launchSettings != null)
                {
                    if (!string.IsNullOrEmpty(launchSettings.ApplicationUrl))
                    {
                        targetCommand.EnvironmentVariable("ASPNETCORE_URLS", launchSettings.ApplicationUrl);
                    }

                    foreach (var entry in launchSettings.EnvironmentVariables)
                    {
                        string value = Environment.ExpandEnvironmentVariables(entry.Value);
                        //NOTE: MSBuild variables are not expanded like they are in VS
                        targetCommand.EnvironmentVariable(entry.Key, value);
                    }
                    if (String.IsNullOrEmpty(targetCommand.CommandArgs) && launchSettings.CommandLineArgs != null)
                    {
                        targetCommand.SetCommandArgs(launchSettings.CommandLineArgs);
                    }
                }

                // Ignore Ctrl-C for the remainder of the command's execution
                Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };

                return targetCommand.Execute().ExitCode;
            }
            catch (InvalidProjectFileException e)
            {
                throw new GracefulException(
                    string.Format(LocalizableStrings.RunCommandSpecifiecFileIsNotAValidProject, Project),
                    e);
            }
        }

        public RunCommand(string configuration,
            string framework,
            string runtime,
            bool noBuild,
            string project,
            string launchProfile,
            bool noLaunchProfile,
            bool noRestore,
            bool interactive,
            IEnumerable<string> restoreArgs,
            IEnumerable<string> args)
        {
            Configuration = configuration;
            Framework = framework;
            Runtime = runtime;
            NoBuild = noBuild;
            Project = project;
            LaunchProfile = launchProfile;
            NoLaunchProfile = noLaunchProfile;
            Args = args;
            RestoreArgs = restoreArgs;
            NoRestore = noRestore;
            Interactive = interactive;
        }

        private bool TryGetLaunchProfileSettingsIfNeeded(out ProjectLaunchSettingsModel launchSettingsModel)
        {
            launchSettingsModel = default;
            if (!UseLaunchProfile)
            {
                return true;
            }

            var buildPathContainer = File.Exists(Project) ? Path.GetDirectoryName(Project) : Project;
            string propsDirectory;

            // VB.NET projects store the launch settings file in the
            // "My Project" directory instead of a "Properties" directory.
            if (string.Equals(Path.GetExtension(Project), ".vbproj", StringComparison.OrdinalIgnoreCase))
            {
                propsDirectory = "My Project";
            }
            else
            {
                propsDirectory = "Properties";
            }

            var launchSettingsPath = Path.Combine(buildPathContainer, propsDirectory, "launchSettings.json");

            if (File.Exists(launchSettingsPath))
            {
                if (!HasQuietVerbosity)
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
                }

                string profileName = string.IsNullOrEmpty(LaunchProfile) ? LocalizableStrings.DefaultLaunchProfileDisplayName : LaunchProfile;

                try
                {
                    var launchSettingsFileContents = File.ReadAllText(launchSettingsPath);
                    var applyResult = LaunchSettingsManager.TryApplyLaunchSettings(launchSettingsFileContents, LaunchProfile);
                    if (!applyResult.Success)
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName, applyResult.FailureReason).Bold().Red());
                    }
                    else
                    {
                        launchSettingsModel = applyResult.LaunchSettings;
                    }
                }
                catch (IOException ex)
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName).Bold().Red());
                    Reporter.Error.WriteLine(ex.Message.Bold().Red());
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(LaunchProfile))
            {
                Reporter.Error.WriteLine(LocalizableStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile.Bold().Red());
            }

            return true;
        }

        private void EnsureProjectIsBuilt()
        {
            var restoreArgs = GetRestoreArguments();

            var buildResult =
                new RestoringCommand(
                    restoreArgs.Prepend(Project),
                    NoRestore,
                    advertiseWorkloadUpdates: false
                ).Execute();

            if (buildResult != 0)
            {
                Reporter.Error.WriteLine();
                throw new GracefulException(LocalizableStrings.RunCommandException);
            }
        }

        private List<string> GetRestoreArguments()
        {
            List<string> args = new List<string>()
            {
                "-nologo"
            };

            // --interactive need to output guide for auth. It cannot be
            // completely "quiet"
            if (!RestoreArgs.Any(a => a.StartsWith("-verbosity:")))
            {
                var defaultVerbosity = Interactive ? "minimal" : "quiet";
                args.Add($"-verbosity:{defaultVerbosity}");
            }

            args.AddRange(RestoreArgs);

            return args;
        }

        private ICommand GetTargetCommand()
        {
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // This property disables default item globbing to improve performance
                // This should be safe because we are not evaluating items, only properties
                { Constants.EnableDefaultItems,    "false" },
                { Constants.MSBuildExtensionsPath, AppContext.BaseDirectory }
            };

            if (!string.IsNullOrWhiteSpace(Configuration))
            {
                globalProperties.Add("Configuration", Configuration);
            }

            if (!string.IsNullOrWhiteSpace(Framework))
            {
                globalProperties.Add("TargetFramework", Framework);
            }

            if (!string.IsNullOrWhiteSpace(Runtime))
            {
                globalProperties.Add("RuntimeIdentifier", Runtime);
            }

            var project = new ProjectInstance(Project, globalProperties, null);

            string runProgram = project.GetPropertyValue("RunCommand");
            if (string.IsNullOrEmpty(runProgram))
            {
                ThrowUnableToRunError(project);
            }

            string runArguments = project.GetPropertyValue("RunArguments");
            string runWorkingDirectory = project.GetPropertyValue("RunWorkingDirectory");

            if (Args.Any())
            {
                runArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(Args);
            }

            CommandSpec commandSpec = new CommandSpec(runProgram, runArguments);

            var command = CommandFactoryUsingResolver.Create(commandSpec)
                .WorkingDirectory(runWorkingDirectory);

            if (((TryGetTargetArchitecture(project.GetPropertyValue("RuntimeIdentifier"), out var targetArchitecture) ||
                TryGetTargetArchitecture(project.GetPropertyValue("DefaultAppHostRuntimeIdentifier"), out targetArchitecture)) &&
                targetArchitecture == RuntimeInformation.ProcessArchitecture) || targetArchitecture == null)
            {
                var rootVariableName = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
                string targetFrameworkVersion = project.GetPropertyValue("TargetFrameworkVersion");
                if (!string.IsNullOrEmpty(targetFrameworkVersion) && Version.Parse(targetFrameworkVersion.AsSpan(1)) >= Version6_0)
                {
                    rootVariableName = $"DOTNET_ROOT_{RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant()}";
                }

                if (Environment.GetEnvironmentVariable(rootVariableName) == null)
                {
                    command.EnvironmentVariable(rootVariableName, Path.GetDirectoryName(new Muxer().MuxerPath));
                }
            }

            return command;
        }

        private void ThrowUnableToRunError(ProjectInstance project)
        {
            string targetFrameworks = project.GetPropertyValue("TargetFrameworks");
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                string targetFramework = project.GetPropertyValue("TargetFramework");
                if (string.IsNullOrEmpty(targetFramework))
                {
                    throw new GracefulException(LocalizableStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework");
                }
            }

            throw new GracefulException(
                    string.Format(
                        LocalizableStrings.RunCommandExceptionUnableToRun,
                        "dotnet run",
                        "OutputType",
                        project.GetPropertyValue("OutputType")));
        }

        private void Initialize()
        {
            if (string.IsNullOrWhiteSpace(Project))
            {
                Project = Directory.GetCurrentDirectory();
            }

            if (Directory.Exists(Project))
            {
                Project = FindSingleProjectInDirectory(Project);
            }
        }

        private static string FindSingleProjectInDirectory(string directory)
        {
            string[] projectFiles = Directory.GetFiles(directory, "*.*proj");

            if (projectFiles.Length == 0)
            {
                throw new GracefulException(LocalizableStrings.RunCommandExceptionNoProjects, directory, "--project");
            }
            else if (projectFiles.Length > 1)
            {
                throw new GracefulException(LocalizableStrings.RunCommandExceptionMultipleProjects, directory);
            }

            return projectFiles[0];
        }

        private static bool TryGetTargetArchitecture(string runtimeIdentifier, out Architecture? targetArchitecture)
        {
            targetArchitecture = null;
            int separator = runtimeIdentifier.LastIndexOf("-", StringComparison.InvariantCulture);
            if (separator < 0)
            {
                return false;
            }

            targetArchitecture = runtimeIdentifier.Substring(separator + 1).ToLowerInvariant() switch
            {
                "arm" => Architecture.Arm,
                "arm64" => Architecture.Arm64,
                "x64" => Architecture.X64,
                "x86" => Architecture.X86,
                _ => null
            };

            return targetArchitecture != null;
        }
    }
}
