// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Repl.Csi
{
    public sealed class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet repl csi";
            app.FullName = "C# REPL";
            app.Description = "C# REPL for the .NET platform";
            app.HelpOption("-h|--help");

            var script = app.Argument("<SCRIPT>", "The .csx file to run. Defaults to interactive mode.");
            var framework = app.Option("-f|--framework <FRAMEWORK>", "Compile a specific framework", CommandOptionType.MultipleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var preserveTemporary = app.Option("-t|--preserve-temporary", "Preserve the temporary directory containing the compiled project.", CommandOptionType.NoValue);
            var project = app.Option("-p|--project <PROJECT>", "The path to the project to run. Can be a path to a project.json or a project directory", CommandOptionType.SingleValue);

            app.OnExecute(() => Run(script.Value, framework.Values, configuration.Value(), preserveTemporary.HasValue(), project.Value()));
            return app.Execute(args);
        }

        private static ProjectContext GetProjectContext(IEnumerable<string> targetFrameworks, string projectPath)
        {
            var contexts = ProjectContext.CreateContextForEachFramework(projectPath);
            var context = contexts.First();

            if (targetFrameworks.Any())
            {
                var framework = NuGetFramework.Parse(targetFrameworks.First());
                context = contexts.FirstOrDefault(c => c.TargetFramework.Equals(framework));
            }

            return context;
        }

        private static CommandResult CompileProject(ProjectContext projectContext, string configuration, out string tempOutputDir)
        {
            tempOutputDir = Path.Combine(projectContext.ProjectDirectory, "bin", ".dotnetrepl", Guid.NewGuid().ToString("N"));

            Reporter.Output.WriteLine($"Compiling {projectContext.RootProject.Identity.Name.Yellow()} for {projectContext.TargetFramework.DotNetFrameworkName.Yellow()} to use with the {"C# REPL".Yellow()} environment.");

            return Command.Create($"dotnet-compile", $"--output \"{tempOutputDir}\" --temp-output \"{tempOutputDir}\" --framework \"{projectContext.TargetFramework}\" --configuration \"{configuration}\" \"{projectContext.ProjectFile.ProjectDirectory}\"")
                                .ForwardStdOut(onlyIfVerbose: true)
                                .ForwardStdErr()
                                .Execute();
        }

        private static IEnumerable<string> GetRuntimeDependencies(ProjectContext projectContext, string buildConfiguration)
        {
            var runtimeDependencies = new HashSet<string>();

            var projectExporter = projectContext.CreateExporter(buildConfiguration);
            var projectDependencies = projectExporter.GetDependencies();

            foreach (var projectDependency in projectDependencies)
            {
                var runtimeAssemblies = projectDependency.RuntimeAssemblies;

                foreach (var runtimeAssembly in runtimeAssemblies)
                {
                    var runtimeAssemblyPath = runtimeAssembly.ResolvedPath;
                    runtimeDependencies.Add(runtimeAssemblyPath);
                }
            }

            return runtimeDependencies;
        }

        private static string CreateResponseFile(ProjectContext projectContext, string buildConfiguration, string tempOutputDir)
        {
            var outputFileName = projectContext.ProjectFile.Name;
            var outputFilePath = Path.Combine(tempOutputDir, $"{outputFileName}{Constants.DynamicLibSuffix}");
            var projectResponseFilePath = Path.Combine(tempOutputDir, $"dotnet-repl.{outputFileName}{Constants.ResponseFileSuffix}");

            var runtimeDependencies = GetRuntimeDependencies(projectContext, buildConfiguration);

            var fileStream = new FileStream(projectResponseFilePath, FileMode.Create);
            using (var streamWriter = new StreamWriter(fileStream))
            {
                streamWriter.WriteLine($"/r:\"{outputFilePath}\"");

                foreach (var projectDependency in runtimeDependencies)
                {
                    streamWriter.WriteLine($"/r:\"{projectDependency}\"");
                }
            }

            return projectResponseFilePath;
        }

        private static int Run(string script, IEnumerable<string> targetFrameworks, string buildConfiguration, bool preserveTemporaryOutput, string projectPath)
        {
            var corerun = Path.Combine(AppContext.BaseDirectory, Constants.HostExecutableName);
            var csiExe = Path.Combine(AppContext.BaseDirectory, "csi.exe");
            var csiArgs = new StringBuilder();

            if (buildConfiguration == null)
            {
                buildConfiguration = Constants.DefaultConfiguration;
            }

            string tempOutputDir = null;

            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                var projectContext = GetProjectContext(targetFrameworks, projectPath);

                if (projectContext == null)
                {
                    Reporter.Error.WriteLine($"Unrecognized framework: {targetFrameworks.First()}".Red());
                }

                var compileResult = CompileProject(projectContext, buildConfiguration, out tempOutputDir);

                if (compileResult.ExitCode != 0)
                {
                    return compileResult.ExitCode;
                }

                string responseFile = CreateResponseFile(projectContext, buildConfiguration, tempOutputDir);
                csiArgs.Append($"@\"{responseFile}\" ");
            }

            csiArgs.Append(string.IsNullOrEmpty(script) ? "-i" : script);

            var result = Command.Create(csiExe, csiArgs.ToString())
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();

            if ((tempOutputDir != null) && !preserveTemporaryOutput)
            {
                Directory.Delete(tempOutputDir, recursive: true);
            }

            return result.ExitCode;
        }
    }
}
