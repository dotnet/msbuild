// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Compiler.Common;
using static Microsoft.DotNet.Cli.Compiler.Common.AssemblyInfoOptions;

namespace Microsoft.DotNet.Tools.Compiler
{
    internal class AssemblyInfoOptionsCommandLine
    {
        private const string ArgTemplate = "<arg>";

        public CommandOption VersionOption { get; set; }
        public CommandOption TitleOption { get; set; }
        public CommandOption DescriptionOption { get; set; }
        public CommandOption CopyrightOption { get; set; }
        public CommandOption NeutralCultureOption { get; set; }
        public CommandOption CultureOption { get; set; }
        public CommandOption InformationalVersionOption { get; set; }
        public CommandOption FileVersionOption { get; set; }
        public CommandOption TargetFrameworkOption { get; set; }

        public static AssemblyInfoOptionsCommandLine AddOptions(CommandLineApplication app)
        {
            AssemblyInfoOptionsCommandLine commandLineOptions = new AssemblyInfoOptionsCommandLine();

            commandLineOptions.VersionOption =
                app.Option($"{AssemblyVersionOptionName} {ArgTemplate}", "Assembly version", CommandOptionType.SingleValue);

            commandLineOptions.TitleOption =
                app.Option($"{TitleOptionName} {ArgTemplate}", "Assembly title", CommandOptionType.SingleValue);

            commandLineOptions.DescriptionOption =
                app.Option($"{DescriptionOptionName} {ArgTemplate}", "Assembly description", CommandOptionType.SingleValue);

            commandLineOptions.CopyrightOption =
                app.Option($"{CopyrightOptionName} {ArgTemplate}", "Assembly copyright", CommandOptionType.SingleValue);

            commandLineOptions.NeutralCultureOption =
                app.Option($"{NeutralCultureOptionName} {ArgTemplate}", "Assembly neutral culture", CommandOptionType.SingleValue);

            commandLineOptions.CultureOption =
                app.Option($"{CultureOptionName} {ArgTemplate}", "Assembly culture", CommandOptionType.SingleValue);

            commandLineOptions.InformationalVersionOption =
                app.Option($"{InformationalVersionOptionName} {ArgTemplate}", "Assembly informational version", CommandOptionType.SingleValue);

            commandLineOptions.FileVersionOption =
                app.Option($"{AssemblyFileVersionOptionName} {ArgTemplate}", "Assembly file version", CommandOptionType.SingleValue);

            commandLineOptions.TargetFrameworkOption =
                app.Option($"{TargetFrameworkOptionName} {ArgTemplate}", "Assembly target framework", CommandOptionType.SingleValue);

            return commandLineOptions;
        }

        public AssemblyInfoOptions GetOptionValues()
        {
            return new AssemblyInfoOptions()
            {
                AssemblyVersion = UnescapeNewlines(VersionOption.Value()),
                Title = UnescapeNewlines(TitleOption.Value()),
                Description = UnescapeNewlines(DescriptionOption.Value()),
                Copyright = UnescapeNewlines(CopyrightOption.Value()),
                NeutralLanguage = UnescapeNewlines(NeutralCultureOption.Value()),
                Culture = UnescapeNewlines(CultureOption.Value()),
                InformationalVersion = UnescapeNewlines(InformationalVersionOption.Value()),
                AssemblyFileVersion = UnescapeNewlines(FileVersionOption.Value()),
                TargetFramework = UnescapeNewlines(TargetFrameworkOption.Value()),
            };
        }

        private static string UnescapeNewlines(string text)
        {
            return text.Replace("\\r", "\r").Replace("\\n", "\n");
        }
    }
}