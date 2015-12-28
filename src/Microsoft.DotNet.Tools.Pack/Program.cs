// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Compiler.Common;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet pack";
            app.FullName = ".NET Packager";
            app.Description = "Packager for the .NET Platform";
            app.HelpOption("-h|--help");

            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to place outputs", CommandOptionType.SingleValue);
            var intermediateOutput = app.Option("-t|--temp-output <OUTPUT_DIR>", "Directory in which to place temporary outputs", CommandOptionType.SingleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var versionSuffix = app.Option("--version-suffix <VERSION_SUFFIX>", "Defines what `*` should be replaced with in version field in project.json", CommandOptionType.SingleValue);
            var project = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                if(!path.EndsWith(Project.FileName))
                {
                    path = Path.Combine(path, Project.FileName);
                }

                if(!File.Exists(path))
                {
                    Reporter.Error.WriteLine($"Unable to find a project.json in {path}");
                    return 1;
                }

                ProjectReaderSettings settings = null;
                if (versionSuffix.HasValue())
                {
                    settings = new ProjectReaderSettings();
                    settings.VersionSuffix = versionSuffix.Value();
                }

                var configValue = configuration.Value() ?? Cli.Utils.Constants.DefaultConfiguration;
                var outputValue = output.Value();

                return TryBuildPackage(path, configValue, outputValue, intermediateOutput.Value(), settings) ? 0 : 1;
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

        private static bool TryBuildPackage(string path, string configuration, string outputValue, string intermediateOutputValue, ProjectReaderSettings settings = null)
        {
            var contexts = ProjectContext.CreateContextForEachFramework(path, settings);
            var project = contexts.First().ProjectFile;

            if (project.Files.SourceFiles.Any())
            {
                var argsBuilder = new StringBuilder();
                argsBuilder.Append($"--configuration {configuration}");

                if (!string.IsNullOrEmpty(outputValue))
                {
                    argsBuilder.Append($" --output \"{outputValue}\"");
                }

                if (!string.IsNullOrEmpty(intermediateOutputValue))
                {
                    argsBuilder.Append($" --temp-output \"{intermediateOutputValue}\"");
                }

                argsBuilder.Append($" \"{path}\"");

                var result = Command.Create("dotnet-build", argsBuilder.ToString())
                       .ForwardStdOut()
                       .ForwardStdErr()
                       .Execute();

                if (result.ExitCode != 0)
                {
                    return false;
                }
            }

            var packDiagnostics = new List<DiagnosticMessage>();

            var mainPackageGenerator = new PackageGenerator(project, configuration, outputValue);
            var symbolsPackageGenerator = new SymbolPackageGenerator(project, configuration, outputValue);

            return mainPackageGenerator.BuildPackage(contexts, packDiagnostics) &&
                symbolsPackageGenerator.BuildPackage(contexts, packDiagnostics);
        }
    }
}