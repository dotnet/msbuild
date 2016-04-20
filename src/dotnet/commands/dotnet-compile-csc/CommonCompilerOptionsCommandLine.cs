// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.ProjectModel;
using static Microsoft.DotNet.Cli.Compiler.Common.CommonCompilerOptionsExtensions;

namespace Microsoft.DotNet.Tools.Compiler
{
    internal class CommonCompilerOptionsCommandLine
    {
        private const string ArgTemplate = "<arg>";

        public CommandOption DefineOption { get; set; }
        public CommandOption SuppressWarningOption { get; set; }
        public CommandOption LanguageVersionOption { get; set; }
        public CommandOption PlatformOption { get; set; }
        public CommandOption AllowUnsafeOption { get; set; }
        public CommandOption WarningsAsErrorsOption { get; set; }
        public CommandOption OptimizeOption { get; set; }
        public CommandOption KeyFileOption { get; set; }
        public CommandOption DelaySignOption { get; set; }
        public CommandOption PublicSignOption { get; set; }
        public CommandOption DebugTypeOption { get; set; }
        public CommandOption EmitEntryPointOption { get; set; }
        public CommandOption GenerateXmlDocumentationOption { get; set; }
        public CommandOption AdditionalArgumentsOption { get; set; }
        public CommandOption OutputNameOption { get; set; }

        public static CommonCompilerOptionsCommandLine AddOptions(CommandLineApplication app)
        {
            CommonCompilerOptionsCommandLine commandLineOptions = new CommonCompilerOptionsCommandLine();

            commandLineOptions.DefineOption =
                app.Option($"{DefineOptionName} {ArgTemplate}...", "Preprocessor definitions", CommandOptionType.MultipleValue);

            commandLineOptions.SuppressWarningOption =
                app.Option($"{SuppressWarningOptionName} {ArgTemplate}...", "Suppresses the specified warning", CommandOptionType.MultipleValue);

            commandLineOptions.LanguageVersionOption =
                app.Option($"{LanguageVersionOptionName} {ArgTemplate}", "The version of the language used to compile", CommandOptionType.SingleValue);

            commandLineOptions.PlatformOption =
                app.Option($"{PlatformOptionName} {ArgTemplate}", "The target platform", CommandOptionType.SingleValue);

            commandLineOptions.AllowUnsafeOption =
                app.Option($"{AllowUnsafeOptionName} {ArgTemplate}", "Allow unsafe code", CommandOptionType.SingleValue);

            commandLineOptions.WarningsAsErrorsOption =
                app.Option($"{WarningsAsErrorsOptionName} {ArgTemplate}", "Turn all warnings into errors", CommandOptionType.SingleValue);

            commandLineOptions.OptimizeOption =
                app.Option($"{OptimizeOptionName} {ArgTemplate}", "Enable compiler optimizations", CommandOptionType.SingleValue);

            commandLineOptions.KeyFileOption =
                app.Option($"{KeyFileOptionName} {ArgTemplate}", "Path to file containing the key to strong-name sign the output assembly", CommandOptionType.SingleValue);

            commandLineOptions.DelaySignOption =
                app.Option($"{DelaySignOptionName} {ArgTemplate}", "Delay-sign the output assembly", CommandOptionType.SingleValue);

            commandLineOptions.PublicSignOption =
                app.Option($"{PublicSignOptionName} {ArgTemplate}", "Public-sign the output assembly", CommandOptionType.SingleValue);

            commandLineOptions.DebugTypeOption =
                app.Option($"{DebugTypeOptionName} {ArgTemplate}", "The type of PDB to emit: portable or full", CommandOptionType.SingleValue);

            commandLineOptions.EmitEntryPointOption =
                app.Option($"{EmitEntryPointOptionName} {ArgTemplate}", "Output an executable console program", CommandOptionType.SingleValue);

            commandLineOptions.GenerateXmlDocumentationOption =
                app.Option($"{GenerateXmlDocumentationOptionName} {ArgTemplate}", "Generate XML documentation file", CommandOptionType.SingleValue);

            commandLineOptions.AdditionalArgumentsOption =
                app.Option($"{AdditionalArgumentsOptionName} {ArgTemplate}...", "Pass the additional argument directly to the compiler", CommandOptionType.MultipleValue);

            commandLineOptions.OutputNameOption =
                app.Option($"{OutputNameOptionName} {ArgTemplate}", "Output assembly name", CommandOptionType.SingleValue);

            return commandLineOptions;
        }

        public CommonCompilerOptions GetOptionValues()
        {
            return new CommonCompilerOptions()
            {
                Defines = DefineOption.Values,
                SuppressWarnings = SuppressWarningOption.Values,
                LanguageVersion = LanguageVersionOption.Value(),
                Platform = PlatformOption.Value(),
                AllowUnsafe = bool.Parse(AllowUnsafeOption.Value()),
                WarningsAsErrors = bool.Parse(WarningsAsErrorsOption.Value()),
                Optimize = bool.Parse(OptimizeOption.Value()),
                KeyFile = KeyFileOption.Value(),
                DelaySign = bool.Parse(DelaySignOption.Value()),
                PublicSign = bool.Parse(PublicSignOption.Value()),
                DebugType = DebugTypeOption.Value(),
                EmitEntryPoint = bool.Parse(EmitEntryPointOption.Value()),
                GenerateXmlDocumentation = bool.Parse(GenerateXmlDocumentationOption.Value()),
                AdditionalArguments = AdditionalArgumentsOption.Values,
                OutputName = OutputNameOption.Value(),
            };
        }
    }
}