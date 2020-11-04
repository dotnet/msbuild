// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolInstallCommandParser
    {
        public static readonly Argument PackageIdArgument = new Argument(LocalizableStrings.PackageIdArgumentName)
        {
            Description = LocalizableStrings.PackageIdArgumentDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option VersionOption = new Option("--version", LocalizableStrings.VersionOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.VersionOptionName)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        };

        public static readonly Option ConfigOption = new Option("--configfile", LocalizableStrings.ConfigFileOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.ConfigFileOptionName)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        };

        public static readonly Option AddSourceOption = new Option("--add-source", LocalizableStrings.AddSourceOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.AddSourceOptionName)
            {
                Arity = ArgumentArity.OneOrMore
            }
        };

        public static readonly Option FrameworkOption = new Option("--framework", LocalizableStrings.FrameworkOptionDescription)
        {
            Argument = new Argument(LocalizableStrings.FrameworkOptionName)
            {
                Arity = ArgumentArity.ExactlyOne
            }
        };

        public static readonly Option VerbosityOption = CommonOptions.VerbosityOption();

        public static Command GetCommand()
        {
            var command = new Command("install", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(ToolAppliedOption.GlobalOption);
            command.AddOption(ToolAppliedOption.LocalOption);
            command.AddOption(ToolAppliedOption.ToolPathOption);
            command.AddOption(VersionOption);
            command.AddOption(ConfigOption);
            command.AddOption(ToolAppliedOption.ToolManifestOption);
            command.AddOption(AddSourceOption);
            command.AddOption(FrameworkOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);

            return command;
        }
    }
}
