using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;
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
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}/{context.RuntimeIdentifier}");

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
                    "publish");
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Compile the project (and transitively, all it's dependencies)
            var result = Command.Create("dotnet-compile", $"--framework {context.TargetFramework.DotNetFrameworkName} \"{context.ProjectFile.ProjectDirectory}\"")
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            if (result.ExitCode != 0)
            {
                return result.ExitCode;
            }

            // Use a library exporter to collect publish assets
            var exporter = context.CreateExporter(configuration);

            // Copy things marked as copy to output (which we don't have yet)
            // so does copy too many things
            CopyContents(context, outputPath);

            foreach (var export in exporter.GetAllExports())
            {
                Reporter.Output.WriteLine($"Publishing {export.Library.Identity.ToString().Green().Bold()} ...");

                PublishFiles(export.RuntimeAssemblies, outputPath);
                PublishFiles(export.NativeLibraries, outputPath);
            }

            // Publishing for windows, TODO(anurse): Publish for Mac/Linux/etc.
            int exitCode;
            if (context.RuntimeIdentifier.StartsWith("win"))
            {
                exitCode = PublishForWindows(context, outputPath);
            }
            else
            {
                exitCode = PublishForUnix(context, outputPath);
            }

            Reporter.Output.WriteLine($"Published to {outputPath}".Green().Bold());
            return exitCode;
        }

        private static int PublishForUnix(ProjectContext context, string outputPath)
        {
            // Locate Hosts
            string hostsPath = Environment.GetEnvironmentVariable(Constants.HostsPathEnvironmentVariable);
            if (string.IsNullOrEmpty(hostsPath))
            {
                hostsPath = AppContext.BaseDirectory;
            }

            var coreConsole = Path.Combine(hostsPath, Constants.CoreConsoleName);
            if (!File.Exists(coreConsole))
            {
                Reporter.Error.WriteLine($"Unable to locate {Constants.CoreConsoleName} in {coreConsole}, use {Constants.HostsPathEnvironmentVariable} to set the path to it.".Red().Bold());
                return 1;
            }

            var coreRun = Path.Combine(hostsPath, Constants.CoreRunName);
            if (!File.Exists(coreRun))
            {
                Reporter.Error.WriteLine($"Unable to locate {Constants.CoreRunName} in {coreConsole}, use {Constants.HostsPathEnvironmentVariable} to set the path to it.".Red().Bold());
                return 1;
            }

            // TEMPORARILY bring CoreConsole and CoreRun along for the ride on it's own (without renaming)
            File.Copy(coreConsole, Path.Combine(outputPath, Constants.CoreConsoleName), overwrite: true);
            File.Copy(coreRun, Path.Combine(outputPath, Constants.CoreRunName), overwrite: true);

            // Use the 'command' field to generate the name
            var outputExe = Path.Combine(outputPath, context.ProjectFile.Name);

            // Write a script that can be used to launch with CoreRun
            var script = $@"#!/usr/bin/env bash
SOURCE=""${{BASH_SOURCE[0]}}""
while [ -h ""$SOURCE"" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
  SOURCE=""$(readlink ""$SOURCE"")""
  [[ $SOURCE != /* ]] && SOURCE=""$DIR/$SOURCE"" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR=""$( cd -P ""$( dirname ""$SOURCE"" )"" && pwd )""
exec ""$DIR/corerun"" ""$DIR/{context.ProjectFile.Name}.exe"" $*
";

            File.WriteAllText(outputExe, script);

            Command.Create("chmod", $"a+x {outputExe}")
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();

            return 0;
        }

        private static int PublishForWindows(ProjectContext context, string outputPath)
        {
            if (context.TargetFramework.IsDesktop())
            {
                return 0;
            }
            // Locate Hosts
            string hostsPath = Environment.GetEnvironmentVariable(Constants.HostsPathEnvironmentVariable);
            if (string.IsNullOrEmpty(hostsPath))
            {
                hostsPath = AppContext.BaseDirectory;
            }

            var coreConsole = Path.Combine(hostsPath, Constants.CoreConsoleName);
            if (!File.Exists(coreConsole))
            {
                Reporter.Error.WriteLine($"Unable to locate {Constants.CoreConsoleName} in {coreConsole}, use {Constants.HostsPathEnvironmentVariable} to set the path to it.".Red().Bold());
                return 1;
            }

            var coreRun = Path.Combine(hostsPath, Constants.CoreRunName);
            if (!File.Exists(coreRun))
            {
                Reporter.Error.WriteLine($"Unable to locate {Constants.CoreRunName} in {coreConsole}, use {Constants.HostsPathEnvironmentVariable} to set the path to it.".Red().Bold());
                return 1;
            }

            // TEMPORARILY bring CoreConsole and CoreRun along for the ride on it's own (without renaming)
            File.Copy(coreConsole, Path.Combine(outputPath, Constants.CoreConsoleName), overwrite: true);
            File.Copy(coreRun, Path.Combine(outputPath, Constants.CoreRunName), overwrite: true);

            var outputExe = Path.Combine(outputPath, context.ProjectFile.Name + Constants.ExeSuffix);

            // Rename the {app}.exe to {app}.dll
            File.Copy(outputExe, Path.ChangeExtension(outputExe, ".dll"), overwrite: true);

            // Change coreconsole.exe to the {app}.exe name
            File.Copy(coreConsole, outputExe, overwrite: true);
            return 0;
        }

        private static void CopyContents(ProjectContext context, string outputPath)
        {
            var sourceFiles = context.ProjectFile.Files.GetFilesForBundling();
            Copy(sourceFiles, context.ProjectDirectory, outputPath);
        }

        private static void Copy(IEnumerable<string> sourceFiles, string sourceDirectory, string targetDirectory)
        {
            if (sourceFiles == null)
            {
                throw new ArgumentNullException(nameof(sourceFiles));
            }

            sourceDirectory = EnsureTrailingSlash(sourceDirectory);
            targetDirectory = EnsureTrailingSlash(targetDirectory);

            foreach (var sourceFilePath in sourceFiles)
            {
                var fileName = Path.GetFileName(sourceFilePath);

                var targetFilePath = sourceFilePath.Replace(sourceDirectory, targetDirectory);
                var targetFileParentFolder = Path.GetDirectoryName(targetFilePath);

                // Create directory before copying a file
                if (!Directory.Exists(targetFileParentFolder))
                {
                    Directory.CreateDirectory(targetFileParentFolder);
                }

                File.Copy(
                    sourceFilePath,
                    targetFilePath,
                    overwrite: true);

                // clear read-only bit if set
                var fileAttributes = File.GetAttributes(targetFilePath);
                if ((fileAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(targetFilePath, fileAttributes & ~FileAttributes.ReadOnly);
                }
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0 || path[path.Length - 1] == trailingCharacter)
            {
                return path;
            }

            return path + trailingCharacter;
        }

        private static void PublishFiles(IEnumerable<string> files, string outputPath)
        {
            foreach (var file in files)
            {
                File.Copy(file, Path.Combine(outputPath, Path.GetFileName(file)), overwrite: true);
            }
        }
    }
}
