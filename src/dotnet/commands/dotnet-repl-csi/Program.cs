// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Repl.Csi
{
    public sealed class ReplCsiCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet repl csi";
            app.FullName = "C# REPL";
            app.Description = "C# REPL for the .NET platform";
            app.HelpOption("-h|--help");

            var script = app.Argument("<SCRIPT>", "The .csx file to run. Defaults to interactive mode");
            var framework = app.Option("-f|--framework <FRAMEWORK>", "Compile a specific framework", CommandOptionType.SingleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var preserveTemporary = app.Option("-t|--preserve-temporary", "Preserve the temporary directory containing the compiled project", CommandOptionType.NoValue);
            var project = app.Option("-p|--project <PROJECT>", "The path to the project to run. Can be a path to a project.json or a project directory", CommandOptionType.SingleValue);

            app.OnExecute(() => Run(script.Value, framework.Value(), configuration.Value(), preserveTemporary.HasValue(), project.Value(), app.RemainingArguments));
            return app.Execute(args);
        }

        private static ProjectContext GetProjectContext(string targetFramework, string projectPath)
        {
            // Selecting the target framework for the project is done in the same manner as dotnet run. If a target framework
            // was specified, we attempt to create a context for that framework (and error out if the framework is unsupported).
            // Otherwise, we pick the first context supported by the project.

            var contexts = ProjectContext.CreateContextForEachFramework(Path.GetFullPath(projectPath));
            var context = contexts.First();

            if (targetFramework != null)
            {
                var framework = NuGetFramework.Parse(targetFramework);
                context = contexts.FirstOrDefault(c => c.TargetFramework.Equals(framework));
            }

            return context;
        }

        private static int CompileProject(ProjectContext projectContext, string configuration, out string tempOutputDir)
        {
            tempOutputDir = Path.Combine(projectContext.ProjectDirectory, "bin", ".dotnetrepl", Guid.NewGuid().ToString("N"));

            Reporter.Output.WriteLine($"Compiling {projectContext.RootProject.Identity.Name.Yellow()} for {projectContext.TargetFramework.DotNetFrameworkName.Yellow()} to use with the {"C# REPL".Yellow()} environment.");

            // --temp-output is actually the intermediate output folder and can be the same as --output for our temporary compilation (`dotnet run` can be seen doing the same)
            return Build.BuildCommand.Run(new[]
                {
                    $"--output",
                    $"{tempOutputDir}",
                    $"--temp-output",
                    $"{tempOutputDir}",
                    $"--framework",
                    $"{projectContext.TargetFramework}",
                    $"--configuration",
                    $"{configuration}",
                    $"{projectContext.ProjectDirectory}"
                });
        }

        private static IEnumerable<string> GetRuntimeDependencies(ProjectContext projectContext, string buildConfiguration)
        {
            // We collect the full list of runtime dependencies here and pass them back so they can be
            // referenced by the REPL environment when seeding the context. It appears that we need to
            // explicitly list the dependencies as they may not exist in the output directory (as is the
            // for library projects) or they may not exist anywhere on the path (e.g. they may only exist
            // in the nuget package that was downloaded for the compilation) or they may be specific to a
            // specific target framework.

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
            var outputFilePath = Path.Combine(tempOutputDir, $"{outputFileName}.dll");
            var projectResponseFilePath = Path.Combine(tempOutputDir, $"dotnet-repl.{outputFileName}{Constants.ResponseFileSuffix}");

            var runtimeDependencies = GetRuntimeDependencies(projectContext, buildConfiguration);

            using (var fileStream = new FileStream(projectResponseFilePath, FileMode.Create))
            {
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine($"/r:\"{outputFilePath}\"");

                    foreach (var projectDependency in runtimeDependencies)
                    {
                        streamWriter.WriteLine($"/r:\"{projectDependency}\"");
                    }
                }
            }

            return projectResponseFilePath;
        }

        private static int Run(string script, string targetFramework, string buildConfiguration, bool preserveTemporaryOutput, string projectPath, IEnumerable<string> remainingArguments)
        {
            var csiArgs = new List<string>();

            if (buildConfiguration == null)
            {
                buildConfiguration = Constants.DefaultConfiguration;
            }

            string tempOutputDir = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(projectPath))
                {
                    var projectContext = GetProjectContext(targetFramework, projectPath);

                    if (projectContext == null)
                    {
                        Reporter.Error.WriteLine($"Unrecognized framework: {targetFramework.First()}".Red());
                    }

                    var compileResult = CompileProject(projectContext, buildConfiguration, out tempOutputDir);

                    if (compileResult != 0)
                    {
                        Reporter.Error.WriteLine($"Project compilation failed. Exiting REPL".Red());
                        return compileResult;
                    }

                    string responseFile = CreateResponseFile(projectContext, buildConfiguration, tempOutputDir);
                    csiArgs.Add($"@{responseFile}");
                }

                if (string.IsNullOrEmpty(script) && !remainingArguments.Any())
                {
                    csiArgs.Add("-i");
                }
                else
                {
                    csiArgs.Add(script);
                }

                csiArgs.AddRange(remainingArguments);

                return Command.Create("csi", csiArgs)
                    .ForwardStdOut()
                    .ForwardStdErr()
                    .Execute()
                    .ExitCode;
            }
            finally
            {
                if ((tempOutputDir != null) && !preserveTemporaryOutput)
                {
                    Directory.Delete(tempOutputDir, recursive: true);
                }
            }
        }
    }
}
