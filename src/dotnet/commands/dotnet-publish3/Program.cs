// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Publish3
{
    public partial class Publish3Command
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet publish3";
            app.FullName = ".NET Publisher";
            app.Description = "Publisher for the .NET Platform";
            app.AllowArgumentSeparator = true;
            app.HelpOption("-h|--help");

            CommandArgument projectArgument = app.Argument("<PROJECT>",
                "The MSBuild project file to publish. If a project file is not specified, MSBuild searches the current" +
                " working directory for a file that has a file extension that ends in `proj` and uses that file.");

            CommandOption frameworkOption = app.Option(
                "-f|--framework <FRAMEWORK>", "Target framework to publish for",
                CommandOptionType.SingleValue);

            CommandOption runtimeOption = app.Option(
                "-r|--runtime <RUNTIME_IDENTIFIER>", "Target runtime to publish for. The default is to publish a portable application.",
                CommandOptionType.SingleValue);

            CommandOption outputOption = app.Option(
                "-o|--output <OUTPUT_DIR>", "Path in which to publish the app",
                CommandOptionType.SingleValue);

            CommandOption configurationOption = app.Option(
                "-c|--configuration <CONFIGURATION>", "Configuration under which to build",
                CommandOptionType.SingleValue);

            CommandOption versionSuffixOption = app.Option(
                "--version-suffix <VERSION_SUFFIX>", "Defines the value for the $(VersionSuffix) property in the project",
                CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                Publish3Command publish = new Publish3Command();

                publish.ProjectPath = projectArgument.Value;
                publish.Framework = frameworkOption.Value();
                publish.Runtime = runtimeOption.Value();
                publish.OutputPath = outputOption.Value();
                publish.Configuration = configurationOption.Value();
                publish.VersionSuffix = versionSuffixOption.Value();
                publish.ExtraMSBuildArguments = app.RemainingArguments;

                return publish.Execute();
            });

            return app.Execute(args);
        }
    }
}
