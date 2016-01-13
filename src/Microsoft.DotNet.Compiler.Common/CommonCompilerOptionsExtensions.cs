// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class CommonCompilerOptionsExtensions
    {
        internal static readonly OptionTemplate s_definesTemplate = new OptionTemplate("define");

        internal static readonly OptionTemplate s_suppressWarningTemplate = new OptionTemplate("suppress-warning");

        internal static readonly OptionTemplate s_languageVersionTemplate = new OptionTemplate("language-version");

        internal static readonly OptionTemplate s_platformTemplate = new OptionTemplate("platform");

        internal static readonly OptionTemplate s_allowUnsafeTemplate = new OptionTemplate("allow-unsafe");

        internal static readonly OptionTemplate s_warningsAsErrorsTemplate = new OptionTemplate("warnings-as-errors");

        internal static readonly OptionTemplate s_optimizeTemplate = new OptionTemplate("optimize");

        internal static readonly OptionTemplate s_keyFileTemplate = new OptionTemplate("key-file");

        internal static readonly OptionTemplate s_delaySignTemplate = new OptionTemplate("delay-sign");

        internal static readonly OptionTemplate s_publicSignTemplate = new OptionTemplate("public-sign");

        internal static readonly OptionTemplate s_emitEntryPointTemplate = new OptionTemplate("emit-entry-point");

        internal static readonly OptionTemplate s_generateXmlDocumentation = new OptionTemplate("generate-xml-documentation");

        public static CommonCompilerOptions Parse(ArgumentSyntax syntax)
        {
            IReadOnlyList<string> defines = null;
            IReadOnlyList<string> suppressWarnings = null;
            string languageVersion = null;
            string platform = null;
            bool? allowUnsafe = null;
            bool? warningsAsErrors = null;
            bool? optimize = null;
            string keyFile = null;
            bool? delaySign = null;
            bool? publicSign = null;
            bool? emitEntryPoint = null;
            bool? generateXmlDocumentation = null;

            Func<string, bool?> nullableBoolConverter = v => bool.Parse(v);

            syntax.DefineOptionList(s_definesTemplate.LongName, ref defines, "Preprocessor definitions");

            syntax.DefineOptionList(s_suppressWarningTemplate.LongName, ref suppressWarnings, "Suppresses the specified warning");

            syntax.DefineOption(s_languageVersionTemplate.LongName, ref languageVersion,
                    "The version of the language used to compile");

            syntax.DefineOption(s_platformTemplate.LongName, ref platform,
                    "The target platform");

            syntax.DefineOption(s_allowUnsafeTemplate.LongName, ref allowUnsafe,
                    nullableBoolConverter, "Allow unsafe code");

            syntax.DefineOption(s_warningsAsErrorsTemplate.LongName, ref warningsAsErrors,
                    nullableBoolConverter, "Turn all warnings into errors");

            syntax.DefineOption(s_optimizeTemplate.LongName, ref optimize,
                    nullableBoolConverter, "Enable compiler optimizations");

            syntax.DefineOption(s_keyFileTemplate.LongName, ref keyFile,
                    "Path to file containing the key to strong-name sign the output assembly");

            syntax.DefineOption(s_delaySignTemplate.LongName, ref delaySign,
                    nullableBoolConverter, "Delay-sign the output assembly");

            syntax.DefineOption(s_publicSignTemplate.LongName, ref publicSign,
                    nullableBoolConverter, "Public-sign the output assembly");

            syntax.DefineOption(s_emitEntryPointTemplate.LongName, ref emitEntryPoint,
                    nullableBoolConverter, "Output an executable console program");

            syntax.DefineOption(s_generateXmlDocumentation.LongName, ref generateXmlDocumentation,
                    nullableBoolConverter, "Generate XML documentation file");

            return new CommonCompilerOptions
            {
                Defines = defines,
                SuppressWarnings = suppressWarnings,
                LanguageVersion = languageVersion,
                Platform = platform,
                AllowUnsafe = allowUnsafe,
                WarningsAsErrors = warningsAsErrors,
                Optimize = optimize,
                KeyFile = keyFile,
                DelaySign = delaySign,
                PublicSign = publicSign,
                EmitEntryPoint = emitEntryPoint,
                GenerateXmlDocumentation = generateXmlDocumentation
            };
        }

        public static IEnumerable<string> SerializeToArgs(this CommonCompilerOptions options)
        {
            var defines = options.Defines;
            var suppressWarnings = options.SuppressWarnings;
            var languageVersion = options.LanguageVersion;
            var platform = options.Platform;
            var allowUnsafe = options.AllowUnsafe;
            var warningsAsErrors = options.WarningsAsErrors;
            var optimize = options.Optimize;
            var keyFile = options.KeyFile;
            var delaySign = options.DelaySign;
            var publicSign = options.PublicSign;
            var emitEntryPoint = options.EmitEntryPoint;
            var generateXmlDocumentation = options.GenerateXmlDocumentation;

            var args = new List<string>();

            if (defines != null)
            {
                args.AddRange(defines.Select(def => s_definesTemplate.ToLongArg(def)));
            }

            if (suppressWarnings != null)
            {
                args.AddRange(suppressWarnings.Select(def => s_suppressWarningTemplate.ToLongArg(def)));
            }

            if (languageVersion != null)
            {
                args.Add(s_languageVersionTemplate.ToLongArg(languageVersion));
            }

            if (platform != null)
            {
                args.Add(s_platformTemplate.ToLongArg(platform));
            }

            if (allowUnsafe != null)
            {
                args.Add(s_allowUnsafeTemplate.ToLongArg(allowUnsafe));
            }

            if (warningsAsErrors != null)
            {
                args.Add(s_warningsAsErrorsTemplate.ToLongArg(warningsAsErrors));
            }

            if (optimize != null)
            {
                args.Add(s_optimizeTemplate.ToLongArg(optimize));
            }

            if (keyFile != null)
            {
                args.Add(s_keyFileTemplate.ToLongArg(keyFile));
            }

            if (delaySign != null)
            {
                args.Add(s_delaySignTemplate.ToLongArg(delaySign));
            }

            if (publicSign != null)
            {
                args.Add(s_publicSignTemplate.ToLongArg(publicSign));
            }

            if (emitEntryPoint != null)
            {
                args.Add(s_emitEntryPointTemplate.ToLongArg(emitEntryPoint));
            }

            if (generateXmlDocumentation != null)
            {
                args.Add(s_generateXmlDocumentation.ToLongArg(generateXmlDocumentation));
            }

            return args;
        }
    }
}
