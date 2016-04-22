// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Publish
{
    public partial class PublishCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet publish";
            app.FullName = ".NET Publisher";
            app.Description = "Publisher for the .NET Platform";
            app.HelpOption("-h|--help");

            var framework = app.Option("-f|--framework <FRAMEWORK>", "Target framework to compile for", CommandOptionType.SingleValue);
            var runtime = app.Option("-r|--runtime <RUNTIME_IDENTIFIER>", "Target runtime to publish for", CommandOptionType.SingleValue);
            var buildBasePath = app.Option("-b|--build-base-path <OUTPUT_DIR>", "Directory in which to place temporary outputs", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_PATH>", "Path in which to publish the app", CommandOptionType.SingleValue);
            var versionSuffix = app.Option("--version-suffix <VERSION_SUFFIX>", "Defines what `*` should be replaced with in version field in project.json", CommandOptionType.SingleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var projectPath = app.Argument("<PROJECT>", "The project to publish, defaults to the current directory. Can be a path to a project.json or a project directory");
            var nativeSubdirectories = app.Option("--native-subdirectory", "Temporary mechanism to include subdirectories from native assets of dependency packages in output", CommandOptionType.NoValue);
            var noBuild = app.Option("--no-build", "Do not build projects before publishing", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                var publish = new PublishCommand();

                publish.Framework = framework.Value();
                publish.Runtime = runtime.Value();
                publish.BuildBasePath = buildBasePath.Value();
                publish.OutputPath = output.Value();
                publish.Configuration = configuration.Value() ?? Constants.DefaultConfiguration;
                publish.NativeSubdirectories = nativeSubdirectories.HasValue();
                publish.ProjectPath = projectPath.Value;
                publish.VersionSuffix = versionSuffix.Value();
                publish.ShouldBuild = !noBuild.HasValue();

                publish.Workspace = WorkspaceContext.Create(versionSuffix.Value(), designTime: false);

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
     }
}
