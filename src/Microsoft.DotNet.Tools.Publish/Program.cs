// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Publish
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet publish";
            app.FullName = ".NET Publisher";
            app.Description = "Publisher for the .NET Platform";
            app.HelpOption("-h|--help");

            var framework = app.Option("-f|--framework <FRAMEWORK>", "Target framework to compile for", CommandOptionType.SingleValue);
            var runtime = app.Option("-r|--runtime <RUNTIME_IDENTIFIER>", "Target runtime to publish for", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_PATH>", "Path in which to publish the app", CommandOptionType.SingleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var project = app.Argument("<PROJECT>", "The project to publish, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                if (framework.HasValue() && IsUnsupportedFramework(framework.Value()))
                {
                    Reporter.Output.WriteLine($"Unsupported framework {framework.Value()}.".Red());
                    return 1;
                }

                // TODO: Remove this once xplat publish is enabled.
                if (!runtime.HasValue())
                {
                    runtime.Values.Add(RuntimeIdentifier.Current);
                }

                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                // get the lock file
                var projectLockJsonPath = GetProjectLockFile(path);
                if (!File.Exists(projectLockJsonPath))
                {
                    Reporter.Output.WriteLine($"Unable to locate {LockFile.FileName} for project '{project.Value}'".Red());
                    Reporter.Output.WriteLine($"Run 'dotnet restore' before calling 'dotnet publish'".Red());
                    return 1;
                }

                var lockFile = LockFileReader.Read(projectLockJsonPath);
                var lockFileTargets = GetMatchingTargets(lockFile, framework.Value(), runtime.Value());

                if(lockFileTargets.Count() == 0)
                {
                    string errMsg = $"'{project.Value}' cannot be published";
                    if(framework.HasValue() || runtime.HasValue())
                    {
                        errMsg += $" for '{framework.Value()}' '{runtime.Value()}'";
                    }

                    Reporter.Output.WriteLine(errMsg.Red());
                    return 1;
                }

                int result = 0;
                foreach(var target in lockFileTargets)
                {
                    result += Publish(target.TargetFramework, target.RuntimeIdentifier, path, output.Value(), configuration.Value() ?? Constants.DefaultConfiguration);
                }

                return result;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

        private static bool CheckArg(CommandOption argument)
        {
            if (!argument.HasValue())
            {
                Reporter.Error.WriteLine($"Missing required argument: {argument.LongName.Red().Bold()}");
                return false;
            }
            return true;
        }

        private static bool IsUnsupportedFramework(string framework)
        {
            return NuGetFramework.Parse(framework).Equals(NuGetFramework.UnsupportedFramework);
        }

        private static string GetProjectDirectory(string project)
        {
            return project.ToLower().EndsWith(Project.FileName) ?
                                    Path.GetDirectoryName(project) :
                                    project;
        }

        private static string GetProjectLockFile(string project)
        {
            string projectDir = GetProjectDirectory(project);
            return Path.Combine(projectDir, LockFile.FileName);
        }

        // return the matching framework/runtime targets in the lock file
        // if 'framework' or 'runtime' is null or empty then it matches with any.
        private static IEnumerable<LockFileTarget> GetMatchingTargets(LockFile lockFile, string framework, string runtime)
        {
            var lockFileTargets = lockFile.Targets.Where(target =>
            {
                if (target.TargetFramework == null || string.IsNullOrEmpty(target.RuntimeIdentifier))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(runtime) || runtime.Equals(target.RuntimeIdentifier))
                {
                    if (string.IsNullOrEmpty(framework) || NuGetFramework.Parse(framework).Equals(target.TargetFramework))
                    {
                        return true;
                    }
                }

                return false;
            });

            return lockFileTargets;
        }

        private static int Publish(NuGetFramework framework, string runtimeIdentifier, string projectPath, string outputPath, string configuration)
        {
            // Load project context and publish it
            var runtimeIdentifiers = new[] { runtimeIdentifier };
            var context = ProjectContext.Create(projectPath, framework, runtimeIdentifiers);

            if (string.IsNullOrEmpty(context.RuntimeIdentifier))
            {
                Reporter.Output.WriteLine($"{context.RootProject.Identity} cannot be published for {context.TargetFramework.DotNetFrameworkName}/{runtimeIdentifier}.".Red());
                return 1;
            }

            return Publish(context, outputPath, configuration);
        }

        private static int Publish(ProjectContext context, string outputPath, string configuration)
        {
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}/{context.RuntimeIdentifier.Yellow()}");

            var options = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);

            // Generate the output path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    context.ProjectFile.ProjectDirectory,
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName(),
                    context.RuntimeIdentifier);
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Compile the project (and transitively, all it's dependencies)
            var result = Command.Create("dotnet-compile", 
                $"--framework \"{context.TargetFramework.DotNetFrameworkName}\" " +
                $"--output \"{outputPath}\" " +
                $"--configuration \"{configuration}\" " +
                "--no-host " +
                $"\"{context.ProjectFile.ProjectDirectory}\"")
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            if (result.ExitCode != 0)
            {
                return result.ExitCode;
            }

            // Use a library exporter to collect publish assets
            var exporter = context.CreateExporter(configuration);

            foreach (var export in exporter.GetAllExports())
            {
                // Skip copying project references
                if (export.Library is ProjectDescription)
                {
                    continue;
                }

                Reporter.Verbose.WriteLine($"Publishing {export.Library.Identity.ToString().Green().Bold()} ...");

                PublishFiles(export.RuntimeAssemblies, outputPath);
                PublishFiles(export.NativeLibraries, outputPath);
            }

            // Publish a host if this is an application
            if (options.EmitEntryPoint.GetValueOrDefault())
            {
                Reporter.Verbose.WriteLine($"Making {context.ProjectFile.Name.Cyan()} runnable ...");
                PublishHost(context, outputPath);
            }

            Reporter.Output.WriteLine($"Published to {outputPath}".Green().Bold());
            return 0;
        }

        private static int PublishHost(ProjectContext context, string outputPath)
        {
            if (context.TargetFramework.IsDesktop())
            {
                return 0;
            }

            var hostPath = Path.Combine(AppContext.BaseDirectory, Constants.HostExecutableName);
            if (!File.Exists(hostPath))
            {
                Reporter.Error.WriteLine($"Cannot find {Constants.HostExecutableName} in the dotnet directory.".Red());
                return 1;
            }

            var outputExe = Path.Combine(outputPath, context.ProjectFile.Name + Constants.ExeSuffix);

            // Copy the host
            File.Copy(hostPath, outputExe, overwrite: true);

            return 0;
        }

        private static void PublishFiles(IEnumerable<LibraryAsset> files, string outputPath)
        {
            foreach (var file in files)
            {
                File.Copy(file.ResolvedPath, Path.Combine(outputPath, Path.GetFileName(file.ResolvedPath)), overwrite: true);
            }
        }
    }
}
