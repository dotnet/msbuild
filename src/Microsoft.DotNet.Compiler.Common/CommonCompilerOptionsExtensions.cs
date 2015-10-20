using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class CommonCompilerOptionsExtensions
    {
        internal static readonly OptionTemplate s_definesTemplate = new OptionTemplate("define");

        internal static readonly OptionTemplate s_languageVersionTemplate = new OptionTemplate("language-version");

        internal static readonly OptionTemplate s_platformTemplate = new OptionTemplate("platform");

        internal static readonly OptionTemplate s_allowUnsafeTemplate = new OptionTemplate("allow-unsafe");

        internal static readonly OptionTemplate s_warningsAsErrorsTemplate = new OptionTemplate("warnings-as-errors");

        internal static readonly OptionTemplate s_optimizeTemplate = new OptionTemplate("optimize");

        internal static readonly OptionTemplate s_keyFileTemplate = new OptionTemplate("key-file");

        internal static readonly OptionTemplate s_delaySignTemplate = new OptionTemplate("delay-sign");

        internal static readonly OptionTemplate s_strongNameTemplate = new OptionTemplate("strong-name");

        internal static readonly OptionTemplate s_emitEntryPointTemplate = new OptionTemplate("emit-entry-point");

        public static CommonCompilerOptions Parse(ArgumentSyntax syntax)
        {
            IReadOnlyList<string> defines = null;
            string languageVersion = null;
            string platform = null;
            bool? allowUnsafe = null;
            bool? warningsAsErrors = null;
            bool? optimize = null;
            string keyFile = null;
            bool? delaySign = null;
            bool? strongName = null;
            bool? emitEntryPoint = null;

            Func<string, bool?> nullableBoolConverter = v => bool.Parse(v);

            syntax.DefineOptionList(s_definesTemplate.LongName, ref defines, "Preprocessor definitions");

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

            syntax.DefineOption(s_strongNameTemplate.LongName, ref strongName,
                    nullableBoolConverter, "Strong-name sign the output assembly");

            syntax.DefineOption(s_emitEntryPointTemplate.LongName, ref emitEntryPoint,
                    nullableBoolConverter, "Output an executable console program");

            return new CommonCompilerOptions
            {
                Defines = defines,
                LanguageVersion = languageVersion,
                Platform = platform,
                AllowUnsafe = allowUnsafe,
                WarningsAsErrors = warningsAsErrors,
                Optimize = optimize,
                KeyFile = keyFile,
                DelaySign = delaySign,
                StrongName = strongName,
                EmitEntryPoint = emitEntryPoint
            };
        }

        public static IEnumerable<string> SerializeToArgs(this CommonCompilerOptions options)
        {
            var defines = options.Defines;
            var languageVersion = options.LanguageVersion;
            var platform = options.Platform;
            var allowUnsafe = options.AllowUnsafe;
            var warningsAsErrors = options.WarningsAsErrors;
            var optimize = options.Optimize;
            var keyFile = options.KeyFile;
            var delaySign = options.DelaySign;
            var strongName = options.StrongName;
            var emitEntryPoint = options.EmitEntryPoint;

            var args = new List<string>();

            if (defines != null)
            {
                args.AddRange(defines.Select(def => s_definesTemplate.ToLongArg(def)));
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

            if (strongName != null)
            {
                args.Add(s_strongNameTemplate.ToLongArg(strongName));
            }

            if (emitEntryPoint != null)
            {
                args.Add(s_emitEntryPointTemplate.ToLongArg(emitEntryPoint));
            }

            return args;
        }
    }
}
