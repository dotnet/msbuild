// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using System;
using System.IO;

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
            var projectPath = app.Argument("<PROJECT>", "The project to publish, defaults to the current directory. Can be a path to a project.json or a project directory");
            var subdirectories = app.Option("--subdir", "Include Subdirectories in native assets in the output", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                var publish = new PublishCommand();

                publish.Framework = framework.Value();
                // TODO: Remove default once xplat publish is enabled.
                publish.Runtime = runtime.Value() ?? RuntimeIdentifier.Current;
                publish.OutputPath = output.Value();
                publish.Configuration = configuration.Value() ?? Constants.DefaultConfiguration;

                publish.ProjectPath = projectPath.Value;
                if (string.IsNullOrEmpty(publish.ProjectPath))
                {
                    publish.ProjectPath = Directory.GetCurrentDirectory();
                }

                if (!publish.TryPrepareForPublish())
                {
                    return 1;
                }

                publish.PublishAllProjects();
                Reporter.Output.WriteLine($"Published {publish.NumberOfPublishedProjects}/{publish.NumberOfProjects} projects successfully");
                return (publish.NumberOfPublishedProjects == publish.NumberOfProjects) ? 0 : 1;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(ex.Message.Red());
                Reporter.Verbose.WriteLine(ex.ToString().Yellow());
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

        // return the matching framework/runtime ProjectContext.
        // if 'nugetframework' or 'runtime' is null or empty then it matches with any.
        private static IEnumerable<ProjectContext> GetMatchingProjectContexts(IEnumerable<ProjectContext> contexts, NuGetFramework framework, string runtimeIdentifier)
        {
            var matchingContexts = contexts.Where(context =>
            {
                if (context.TargetFramework == null || string.IsNullOrEmpty(context.RuntimeIdentifier))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(runtimeIdentifier) || runtimeIdentifier.Equals(context.RuntimeIdentifier))
                {
                    if (framework == null || framework.Equals(context.TargetFramework))
                    {
                        return true;
                    }
                }

                return false;
            });

            return matchingContexts;
        }

        /// <summary>
        /// Publish the project for given 'framework (ex - dnxcore50)' and 'runtimeID (ex - win7-x64)'
        /// </summary>
        /// <param name="context">project that is to be published</param>
        /// <param name="outputPath">Location of published files</param>
        /// <param name="configuration">Debug or Release</param>
        /// <returns>Return 0 if successful else return non-zero</returns>
        private static int Publish(ProjectContext context, string outputPath, string configuration, bool subdirectories)
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

                PublishFiles(export.RuntimeAssemblies, outputPath, false);
                PublishFiles(export.NativeLibraries, outputPath, subdirectories);
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

        private static void PublishFiles(IEnumerable<LibraryAsset> files, string outputPath, bool subdirectories)
        {
            foreach (var file in files)
            {
                var destinationDirectory = outputPath;

                if (subdirectories)
                {
                    destinationDirectory = Path.Combine(outputPath, GetNativeRelativeSubdirectory(file.RelativePath));
                }

                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(file.ResolvedPath, Path.Combine(destinationDirectory, Path.GetFileName(file.ResolvedPath)), overwrite: true);
            }
        }

        private static string GetNativeRelativeSubdirectory(string filepath)
        {
            string directoryPath = Path.GetDirectoryName(filepath);

            string[] parts = directoryPath.Split(new string[] { "native" }, 2, StringSplitOptions.None);

            if (parts.Length != 2)
            {
                throw new UnrecognizedNativeDirectoryFormat() { FailedPath = filepath };
            }

            string candidate = parts[1];
            candidate = candidate.TrimStart(new char[] { '/', '\\' });

            return candidate;
        }

        private class UnrecognizedNativeDirectoryFormat : Exception
        {
            public string FailedPath { get; set; }
        }
    }

    
}
