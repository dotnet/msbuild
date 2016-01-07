// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Pack;

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

            var basePath = app.Option("-b|--basepath <BASE_PATH>", "Directory from where the assets to be packaged are going to be picked up", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to place outputs", CommandOptionType.SingleValue);
            var intermediateOutput = app.Option("-t|--temp-output <OUTPUT_DIR>", "Directory in which to place temporary outputs", CommandOptionType.SingleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var versionSuffix = app.Option("--version-suffix <VERSION_SUFFIX>", "Defines what `*` should be replaced with in version field in project.json", CommandOptionType.SingleValue);
            var path = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                var pathValue = path.Value;
                if (string.IsNullOrEmpty(pathValue))
                {
                    pathValue = Directory.GetCurrentDirectory();
                }

                if(!pathValue.EndsWith(Project.FileName))
                {
                    pathValue = Path.Combine(pathValue, Project.FileName);
                }

                if(!File.Exists(pathValue))
                {
                    Reporter.Error.WriteLine($"Unable to find a project.json in {pathValue}");
                    return 1;
                }

                // Set defaults based on the environment
                var settings = new ProjectReaderSettings();
                settings.VersionSuffix = Environment.GetEnvironmentVariable("DOTNET_BUILD_VERSION");
                settings.AssemblyFileVersion = Environment.GetEnvironmentVariable("DOTNET_ASSEMBLY_FILE_VERSION");

                if (versionSuffix.HasValue())
                {
                    settings.VersionSuffix = versionSuffix.Value();
                }

                var contexts = ProjectContext.CreateContextForEachFramework(pathValue, settings);

                var configValue = configuration.Value() ?? Cli.Utils.Constants.DefaultConfiguration;
                var basePathValue = basePath.Value();
                var outputValue = output.Value();
                var intermediateOutputValue = intermediateOutput.Value();

                var project = contexts.First().ProjectFile;

                var artifactPathsCalculator = new ArtifactPathsCalculator(project, basePathValue, outputValue, configValue);
                var buildProjectCommand = new BuildProjectCommand(project, artifactPathsCalculator, intermediateOutputValue, configValue);
                var packageBuilder = new PackagesGenerator(contexts, artifactPathsCalculator, configValue);

                var buildResult = buildProjectCommand.Execute();
                return buildResult != 0 ? buildResult : packageBuilder.Build();
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
    }
}