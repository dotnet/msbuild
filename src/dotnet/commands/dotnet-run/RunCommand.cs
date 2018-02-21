// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Run.LaunchSettings;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public string Configuration { get; private set; }
        public string Framework { get; private set; }
        public bool NoBuild { get; private set; }
        public string Project { get; private set; }
        public IReadOnlyCollection<string> Args { get; private set; }
        public bool NoRestore { get; private set; }
        public IEnumerable<string> RestoreArgs { get; private set; }

        private List<string> _args;
        private bool ShouldBuild => !NoBuild;

        public string LaunchProfile { get; private set; }
        public bool NoLaunchProfile { get; private set; }
        private bool UseLaunchProfile => !NoLaunchProfile;

        public int Start()
        {
            Initialize();

            if (ShouldBuild)
            {
                EnsureProjectIsBuilt();
            }

            try
            {
                ICommand runCommand = GetRunCommand();
                int launchSettingsApplicationResult = ApplyLaunchProfileSettingsIfNeeded(ref runCommand);

                if (launchSettingsApplicationResult != 0)
                {
                    return launchSettingsApplicationResult;
                }

                return runCommand
                    .Execute()
                    .ExitCode;
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
            bool noBuild,
            string project,
            string launchProfile,
            bool noLaunchProfile,
            bool noRestore,
            IEnumerable<string> restoreArgs,
            IReadOnlyCollection<string> args)
        {
            Configuration = configuration;
            Framework = framework;
            NoBuild = noBuild;
            Project = project;
            LaunchProfile = launchProfile;
            NoLaunchProfile = noLaunchProfile;
            Args = args;
            RestoreArgs = restoreArgs;
            NoRestore = noRestore;
        }

        public RunCommand MakeNewWithReplaced(string configuration = null,
            string framework = null,
            bool? noBuild = null,
            string project = null,
            string launchProfile = null,
            bool? noLaunchProfile = null,
            bool? noRestore = null,
            IEnumerable<string> restoreArgs = null,
            IReadOnlyCollection<string> args = null)
        {
            return new RunCommand(
                configuration ?? this.Configuration,
                framework ?? this.Framework,
                noBuild ?? this.NoBuild,
                project ?? this.Project,
                launchProfile ?? this.LaunchProfile,
                noLaunchProfile ?? this.NoLaunchProfile,
                noRestore ?? this.NoRestore,
                restoreArgs ?? this.RestoreArgs,
                args ?? this.Args
            );
        }

        private int ApplyLaunchProfileSettingsIfNeeded(ref ICommand runCommand)
        {
            if (UseLaunchProfile)
            {
                var buildPathContainer = File.Exists(Project) ? Path.GetDirectoryName(Project) : Project;
                var launchSettingsPath = Path.Combine(buildPathContainer, "Properties", "launchSettings.json");
                if (File.Exists(launchSettingsPath))
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
                    string profileName = string.IsNullOrEmpty(LaunchProfile) ? LocalizableStrings.DefaultLaunchProfileDisplayName : LaunchProfile;

                    try
                    {
                        var launchSettingsFileContents = File.ReadAllText(launchSettingsPath);
                        var applyResult = LaunchSettingsManager.TryApplyLaunchSettings(launchSettingsFileContents, ref runCommand, LaunchProfile);
                        if (!applyResult.Success)
                        {                            
                            //Error that the launch profile couldn't be applied
                            Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName, applyResult.FailureReason).Bold().Red());
                        }
                    }
                    catch (IOException ex)
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName).Bold().Red());
                        Reporter.Error.WriteLine(ex.Message.Bold().Red());
                        return -1;
                    }
                }
                else if (!string.IsNullOrEmpty(LaunchProfile))
                {
                    //Error that the launch profile couldn't be found
                    Reporter.Error.WriteLine(LocalizableStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile.Bold().Red());
                }
            }

            return 0;
        }

        private void EnsureProjectIsBuilt()
        {
            var restoreArgs = GetRestoreArguments();

            var buildResult =
                new RestoringCommand(
                    restoreArgs.Prepend(Project),
                    restoreArgs,
                    new [] { Project },
                    NoRestore
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
                "/nologo"
            };

            if (!RestoreArgs.Any(a => a.StartsWith("/verbosity:")))
            {
                args.Add("/verbosity:quiet");
            }

            args.AddRange(RestoreArgs);

            return args;
        }

        private ICommand GetRunCommand()
        {
            var globalProperties = new Dictionary<string, string>
            {
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

            Project project = new Project(Project, globalProperties, null);

            string runProgram = project.GetPropertyValue("RunCommand");
            if (string.IsNullOrEmpty(runProgram))
            {
                ThrowUnableToRunError(project);
            }

            string runArguments = project.GetPropertyValue("RunArguments");
            string runWorkingDirectory = project.GetPropertyValue("RunWorkingDirectory");

            string fullArguments = runArguments;
            if (_args.Any())
            {
                fullArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(_args);
            }

            CommandSpec commandSpec = new CommandSpec(runProgram, fullArguments, CommandResolutionStrategy.None);

            return Command.Create(commandSpec)
                .WorkingDirectory(runWorkingDirectory);
        }

        private void ThrowUnableToRunError(Project project)
        {
            string targetFrameworks = project.GetPropertyValue("TargetFrameworks");
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                string targetFramework = project.GetPropertyValue("TargetFramework");
                if (string.IsNullOrEmpty(targetFramework))
                {
                    var framework = "--framework";

                    throw new GracefulException(LocalizableStrings.RunCommandExceptionUnableToRunSpecifyFramework, framework);
                }
            }

            string outputType = project.GetPropertyValue("OutputType");

            throw new GracefulException(
                    string.Format(
                        LocalizableStrings.RunCommandExceptionUnableToRun,
                        "dotnet run",
                        "OutputType",
                        outputType));
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

            if (Args == null)
            {
                _args = new List<string>();
            }
            else
            {
                _args = new List<string>(Args);
            }
        }

        private static string FindSingleProjectInDirectory(string directory)
        {
            string[] projectFiles = Directory.GetFiles(directory, "*.*proj");

            if (projectFiles.Length == 0)
            {
                var project = "--project";

                throw new GracefulException(LocalizableStrings.RunCommandExceptionNoProjects, directory, project);
            }
            else if (projectFiles.Length > 1)
            {
                throw new GracefulException(LocalizableStrings.RunCommandExceptionMultipleProjects, directory);
            }

            return projectFiles[0];
        }
    }
}
