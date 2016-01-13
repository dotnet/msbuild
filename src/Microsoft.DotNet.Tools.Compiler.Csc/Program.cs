// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Compiler.Csc
{
    public class Program
    {
        private const int ExitFailed = 1;

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommonCompilerOptions commonOptions = null;
            AssemblyInfoOptions assemblyInfoOptions = null;
            string tempOutDir = null;
            IReadOnlyList<string> references = Array.Empty<string>();
            IReadOnlyList<string> resources = Array.Empty<string>();
            IReadOnlyList<string> sources = Array.Empty<string>();
            string outputName = null;
            var help = false;
            var returnCode = 0;
            string helpText = null;

            try
            {
                ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.HandleHelp = false;
                    syntax.HandleErrors = false;

                    commonOptions = CommonCompilerOptionsExtensions.Parse(syntax);

                    assemblyInfoOptions = AssemblyInfoOptions.Parse(syntax);

                    syntax.DefineOption("temp-output", ref tempOutDir, "Compilation temporary directory");

                    syntax.DefineOption("out", ref outputName, "Name of the output assembly");

                    syntax.DefineOptionList("reference", ref references, "Path to a compiler metadata reference");

                    syntax.DefineOptionList("resource", ref resources, "Resources to embed");

                    syntax.DefineOption("h|help", ref help, "Help for compile native.");

                    syntax.DefineParameterList("source-files", ref sources, "Compilation sources");

                    helpText = syntax.GetHelpText();

                    if (tempOutDir == null)
                    {
                        syntax.ReportError("Option '--temp-output' is required");
                    }
                });
            }
            catch (ArgumentSyntaxException exception)
            {
                Console.Error.WriteLine(exception.Message);
                help = true;
                returnCode = ExitFailed;
            }

            if (help)
            {
                Console.WriteLine(helpText);

                return returnCode;
            }

            var translated = TranslateCommonOptions(commonOptions, outputName);

            var allArgs = new List<string>(translated);
            allArgs.AddRange(GetDefaultOptions());

            // Generate assembly info
            var assemblyInfo = Path.Combine(tempOutDir, $"dotnet-compile.assemblyinfo.cs");
            File.WriteAllText(assemblyInfo, AssemblyInfoFileGenerator.Generate(assemblyInfoOptions, sources));
            allArgs.Add($"\"{assemblyInfo}\"");

            if (outputName != null)
            {
                allArgs.Add($"-out:\"{outputName}\"");
            }

            allArgs.AddRange(references.Select(r => $"-r:\"{r}\""));
            allArgs.AddRange(resources.Select(resource => $"-resource:{resource}"));
            allArgs.AddRange(sources.Select(s => $"\"{s}\""));

            var rsp = Path.Combine(tempOutDir, "dotnet-compile-csc.rsp");

            File.WriteAllLines(rsp, allArgs, Encoding.UTF8);

            // Execute CSC!
            var result = RunCsc($"-noconfig @\"{rsp}\"")
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            return result.ExitCode;
        }

        // TODO: Review if this is the place for default options
        private static IEnumerable<string> GetDefaultOptions()
        {
            var args = new List<string>()
            {
                "-nostdlib",
                "-nologo"
            };

            args.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "-debug:full"
                : "-debug:portable");

            args.Add("-nowarn:CS1701");
            args.Add("-nowarn:CS1702");
            args.Add("-nowarn:CS1705");

            return args;
        }

        private static IEnumerable<string> TranslateCommonOptions(CommonCompilerOptions options, string outputName)
        {
            List<string> commonArgs = new List<string>();

            if (options.Defines != null)
            {
                commonArgs.AddRange(options.Defines.Select(def => $"-d:{def}"));
            }

            if (options.SuppressWarnings != null)
            {
                commonArgs.AddRange(options.SuppressWarnings.Select(w => $"-nowarn:{w}"));
            }

            if (options.LanguageVersion != null)
            {
                commonArgs.Add($"-langversion:{GetLanguageVersion(options.LanguageVersion)}");
            }

            if (options.Platform != null)
            {
                commonArgs.Add($"-platform:{options.Platform}");
            }

            if (options.AllowUnsafe == true)
            {
                commonArgs.Add("-unsafe");
            }

            if (options.WarningsAsErrors == true)
            {
                commonArgs.Add("-warnaserror");
            }

            if (options.Optimize == true)
            {
                commonArgs.Add("-optimize");
            }

            if (options.KeyFile != null)
            {
                commonArgs.Add($"-keyfile:\"{options.KeyFile}\"");

                // If we're not on Windows, full signing isn't supported, so we'll
                // public sign, unless the public sign switch has explicitly been
                // set to false
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    options.PublicSign == null)
                {
                    commonArgs.Add("-publicsign");
                }
            }

            if (options.DelaySign == true)
            {
                commonArgs.Add("-delaysign");
            }

            if (options.PublicSign == true)
            {
                commonArgs.Add("-publicsign");
            }

            if (options.GenerateXmlDocumentation == true)
            {
                commonArgs.Add($"-doc:{Path.ChangeExtension(outputName, "xml")}");
            }

            if (options.EmitEntryPoint != true)
            {
                commonArgs.Add("-t:library");
            }

            return commonArgs;
        }

        private static string GetLanguageVersion(string languageVersion)
        {
            // project.json supports the enum that the roslyn APIs expose
            if (languageVersion?.StartsWith("csharp", StringComparison.OrdinalIgnoreCase) == true)
            {
                // We'll be left with the number csharp6 = 6
                return languageVersion.Substring("csharp".Length);
            }
            return languageVersion;
        }

        private static Command RunCsc(string cscArgs)
        {
            // Locate CoreRun
            return Command.Create("csc", cscArgs);
        }
    }
}
