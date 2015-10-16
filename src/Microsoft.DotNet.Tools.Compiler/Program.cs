using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Compilation;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet compile";
            app.FullName = ".NET Compiler";
            app.Description = "Compiler for the .NET Platform";
            app.HelpOption("-h|--help");

            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to place outputs", CommandOptionType.SingleValue);
            var framework = app.Option("-f|--framework <FRAMEWORK>", "Compile a specific framework", CommandOptionType.MultipleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var project = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                // Load project contexts for each framework and compile them
                bool success = true;
                if (framework.HasValue())
                {
                    foreach (var context in framework.Values.Select(f => ProjectContext.Create(path, NuGetFramework.Parse(f))))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value());
                    }
                }
                else
                {
                    foreach (var context in ProjectContext.CreateContextForEachFramework(path))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value());
                    }
                }
                return success ? 0 : 1;
            });

            try
            {
                return app.Execute(args);
            }
            catch (OperationCanceledException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static bool Compile(ProjectContext context, string configuration, string outputPath)
        {
            Reporter.Output.WriteLine($"Building {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}");

            // Create the library exporter
            var exporter = context.CreateExporter(configuration);

            // Gather exports for the project
            var dependencies = exporter.GetCompilationDependencies().ToList();

            // Hackily trigger builds of the dependent projects
            foreach (var dependency in dependencies.Where(d => d.CompilationAssemblies.Any()))
            {
                var projectDependency = dependency.Library as ProjectDescription;
                if (projectDependency != null && !string.Equals(projectDependency.Identity.Name, context.RootProject.Identity.Name, StringComparison.Ordinal))
                {
                    var compileResult = Command.Create("dotnet-compile", $"--framework {projectDependency.Framework} --configuration {configuration} {projectDependency.Project.ProjectDirectory}")
                        .ForwardStdOut()
                        .ForwardStdErr()
                        .RunAsync()
                        .Result;
                    if (compileResult.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"Failed to compile dependency: {projectDependency.Identity.Name}");
                        return false;
                    }
                }
            }

            // Dump dependency data
            ShowDependencyInfo(dependencies);

            // Hackily generate the output path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    context.ProjectFile.ProjectDirectory,
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Get compilation options
            var compilationOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);
            var outputName = Path.Combine(outputPath, context.ProjectFile.Name + ".dll");

            // Assemble csc args
            var cscArgs = new List<string>()
            {
                "-nostdlib",
                "-nologo",
                $"-out:\"{outputName}\""
            };

            // Add compilation options to the args
            ApplyCompilationOptions(compilationOptions, cscArgs);

            foreach (var dependency in dependencies)
            {
                cscArgs.AddRange(dependency.CompilationAssemblies.Select(r => $"-r:\"{r}\""));
                cscArgs.AddRange(dependency.SourceReferences);
            }

            // Add project source files
            cscArgs.AddRange(context.ProjectFile.Files.SourceFiles);

            // Write RSP file
            var rsp = Path.Combine(outputPath, "dotnet-compile.csc.rsp");
            File.WriteAllLines(rsp, cscArgs);

            // Execute CSC!
            var result = RunCsc($"-noconfig @\"{rsp}\"")
                .ForwardStdErr()
                .ForwardStdOut()
                .RunAsync()
                .Result;
            return result.ExitCode == 0;
        }

        private static Command RunCsc(string cscArgs)
        {
            // Locate CoreRun
            string hostRoot = Environment.GetEnvironmentVariable(Constants.HostsPathEnvironmentVariable);
            if(string.IsNullOrEmpty(hostRoot))
            {
                hostRoot = AppContext.BaseDirectory;
            }
            var corerun = Path.Combine(hostRoot, "CoreRun.exe");
            var cscExe = Path.Combine(AppContext.BaseDirectory, "csc.exe");
            return File.Exists(corerun) && File.Exists(cscExe)
                ? Command.Create(corerun, $@"""{cscExe}"" {cscArgs}")
                : Command.Create("csc.exe", cscArgs);
        }

        private static void ApplyCompilationOptions(CompilerOptions compilationOptions, List<string> cscArgs)
        {
            var targetType = compilationOptions.EmitEntryPoint.GetValueOrDefault() ? "exe" : "library";
            cscArgs.Add($"-target:{targetType}");
            if (compilationOptions.AllowUnsafe.GetValueOrDefault())
            {
                cscArgs.Add("-unsafe+");
            }
            cscArgs.AddRange(compilationOptions.Defines.Select(d => $"-d:{d}"));
            if (compilationOptions.Optimize.GetValueOrDefault())
            {
                cscArgs.Add("-optimize");
            }
            if (!string.IsNullOrEmpty(compilationOptions.Platform))
            {
                cscArgs.Add($"-platform:{compilationOptions.Platform}");
            }
            if (compilationOptions.WarningsAsErrors.GetValueOrDefault())
            {
                cscArgs.Add("-warnaserror");
            }
        }

        private static void ShowDependencyInfo(IEnumerable<LibraryExport> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                if (!dependency.Library.Resolved)
                {
                    Reporter.Error.WriteLine($"  Unable to resolve dependency {dependency.Library.Identity.ToString().Red().Bold()}");
                    Reporter.Error.WriteLine("");
                }
                else
                {
                    Reporter.Output.WriteLine($"  Using {dependency.Library.Identity.Type.Value.Cyan().Bold()} dependency {dependency.Library.Identity.ToString().Cyan().Bold()}");
                    Reporter.Output.WriteLine($"    Path: {dependency.Library.Path}");

                    foreach (var metadataReference in dependency.CompilationAssemblies)
                    {
                        Reporter.Output.WriteLine($"    Assembly: {metadataReference}");
                    }

                    foreach (var sourceReference in dependency.SourceReferences)
                    {
                        Reporter.Output.WriteLine($"    Source: {sourceReference}");
                    }
                    Reporter.Output.WriteLine("");
                }
            }
        }
    }
}
