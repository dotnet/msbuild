// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
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
                if (!CheckArg(framework))
                {
                    return 1;
                }
                if (!CheckArg(runtime))
                {
                    return 1;
                }

                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                // Load project context and publish it
                var fx = NuGetFramework.Parse(framework.Value());
                var rids = new[] { runtime.Value() };
                var context = ProjectContext.Create(path, fx, rids);

                if (string.IsNullOrEmpty(context.RuntimeIdentifier))
                {
                    Reporter.Output.WriteLine($"Unknown runtime identifier {runtime.Value()}.".Red());
                    return 1;
                }

                return Publish(context, output.Value(), configuration.Value() ?? Constants.DefaultConfiguration);
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

        private static int Publish(ProjectContext context, string outputPath, string configuration)
        {
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}/{context.RuntimeIdentifier.Yellow()}");

            var options = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);

            if (!options.EmitEntryPoint.GetValueOrDefault())
            {
                Reporter.Output.WriteLine($"{context.RootProject.Identity} does not have an entry point defined.".Red());
                return 1;
            }

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
            var result = Command.Create("dotnet-compile", $"--framework \"{context.TargetFramework.DotNetFrameworkName}\" --output \"{outputPath}\" --configuration \"{configuration}\" \"{context.ProjectFile.ProjectDirectory}\"")
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

            // Publish the application itself
            PublishHost(context, outputPath);

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
