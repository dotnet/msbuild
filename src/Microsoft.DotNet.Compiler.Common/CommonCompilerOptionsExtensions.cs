// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class CommonCompilerOptionsExtensions
    {
        public static readonly string DefineOptionName = "define";
        public static readonly string SuppressWarningOptionName = "suppress-warning";
        public static readonly string LanguageVersionOptionName = "language-version";
        public static readonly string PlatformOptionName = "platform";
        public static readonly string AllowUnsafeOptionName = "allow-unsafe";
        public static readonly string WarningsAsErrorsOptionName = "warnings-as-errors";
        public static readonly string OptimizeOptionName = "optimize";
        public static readonly string KeyFileOptionName = "key-file";
        public static readonly string DelaySignOptionName = "delay-sign";
        public static readonly string PublicSignOptionName = "public-sign";
        public static readonly string DebugTypeOptionName = "debug-type";
        public static readonly string EmitEntryPointOptionName = "emit-entry-point";
        public static readonly string GenerateXmlDocumentationOptionName = "generate-xml-documentation";
        public static readonly string AdditionalArgumentsOptionName = "additional-argument";
        public static readonly string OutputNameOptionName = "output-name";

        internal static readonly OptionTemplate s_definesTemplate = new OptionTemplate(DefineOptionName);

        internal static readonly OptionTemplate s_suppressWarningTemplate = new OptionTemplate(SuppressWarningOptionName);

        internal static readonly OptionTemplate s_languageVersionTemplate = new OptionTemplate(LanguageVersionOptionName);

        internal static readonly OptionTemplate s_platformTemplate = new OptionTemplate(PlatformOptionName);

        internal static readonly OptionTemplate s_allowUnsafeTemplate = new OptionTemplate(AllowUnsafeOptionName);

        internal static readonly OptionTemplate s_warningsAsErrorsTemplate = new OptionTemplate(WarningsAsErrorsOptionName);

        internal static readonly OptionTemplate s_optimizeTemplate = new OptionTemplate(OptimizeOptionName);

        internal static readonly OptionTemplate s_keyFileTemplate = new OptionTemplate(KeyFileOptionName);

        internal static readonly OptionTemplate s_delaySignTemplate = new OptionTemplate(DelaySignOptionName);

        internal static readonly OptionTemplate s_publicSignTemplate = new OptionTemplate(PublicSignOptionName);

        internal static readonly OptionTemplate s_debugTypeTemplate = new OptionTemplate(DebugTypeOptionName);

        internal static readonly OptionTemplate s_emitEntryPointTemplate = new OptionTemplate(EmitEntryPointOptionName);

        internal static readonly OptionTemplate s_generateXmlDocumentation = new OptionTemplate(GenerateXmlDocumentationOptionName);

        internal static readonly OptionTemplate s_additionalArgumentsTemplate = new OptionTemplate(AdditionalArgumentsOptionName);

        internal static readonly OptionTemplate s_outputNameTemplate = new OptionTemplate(OutputNameOptionName);

        public static IEnumerable<string> SerializeToArgs(this CommonCompilerOptions options)
        {
            var defines = options.Defines;
            var suppressWarnings = options.SuppressWarnings;
            var languageVersion = options.LanguageVersion;
            var debugType = options.DebugType;
            var platform = options.Platform;
            var allowUnsafe = options.AllowUnsafe;
            var warningsAsErrors = options.WarningsAsErrors;
            var optimize = options.Optimize;
            var keyFile = options.KeyFile;
            var delaySign = options.DelaySign;
            var publicSign = options.PublicSign;
            var emitEntryPoint = options.EmitEntryPoint;
            var generateXmlDocumentation = options.GenerateXmlDocumentation;
            var outputName = options.OutputName;
            var additionalArguments = options.AdditionalArguments;

            var args = new List<string>();

            if (defines != null)
            {
                args.AddRange(defines.Select(def => s_definesTemplate.ToLongArg(def)));
            }

            if (suppressWarnings != null)
            {
                args.AddRange(suppressWarnings.Select(def => s_suppressWarningTemplate.ToLongArg(def)));
            }

            if (additionalArguments != null)
            {
                args.AddRange(additionalArguments.Select(arg => s_additionalArgumentsTemplate.ToLongArg(arg)));
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

            if (debugType != null)
            {
                args.Add(s_debugTypeTemplate.ToLongArg(debugType));
            }

            if (emitEntryPoint != null)
            {
                args.Add(s_emitEntryPointTemplate.ToLongArg(emitEntryPoint));
            }

            if (generateXmlDocumentation != null)
            {
                args.Add(s_generateXmlDocumentation.ToLongArg(generateXmlDocumentation));
            }

            if (outputName != null)
            {
                args.Add(s_outputNameTemplate.ToLongArg(outputName));
            }

            return args;
        }
    }
}
