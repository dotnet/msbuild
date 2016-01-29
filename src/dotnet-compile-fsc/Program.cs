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

namespace Microsoft.DotNet.Tools.Compiler.Fsc
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
            var assemblyInfo = Path.Combine(tempOutDir, $"dotnet-compile.assemblyinfo.fs");
            File.WriteAllText(assemblyInfo, AssemblyInfoFileGenerator.GenerateFSharp(assemblyInfoOptions));
            allArgs.Add($"{assemblyInfo}");

            //HACK fsc raise error FS0208 if target exe doesnt have extension .exe
            bool hackFS0208 = commonOptions.EmitEntryPoint == true;
            string originalOutputName = outputName;

            if (outputName != null)
            {
                if (hackFS0208)
                {
                    outputName = Path.ChangeExtension(outputName, ".exe");
                }

                allArgs.Add($"--out:");
                allArgs.Add($"{outputName}");
            }

            foreach (var reference in references)
            {
                allArgs.Add("-r");
                allArgs.Add($"{reference}");
            }

            foreach (var resource in resources)
            {
                allArgs.Add("--resource");
                allArgs.Add($"{resource}");
            }

            foreach (var source in sources)
            {
                allArgs.Add($"{source}");
            }

            var rsp = Path.Combine(tempOutDir, "dotnet-compile-fsc.rsp");
            File.WriteAllLines(rsp, allArgs, Encoding.UTF8);

            // Execute FSC!
            var result = RunFsc(allArgs)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            if (hackFS0208 && File.Exists(outputName))
            {
                if (File.Exists(originalOutputName))
                    File.Delete(originalOutputName);
                File.Move(outputName, originalOutputName);
            }

            return result.ExitCode;
        }

        // TODO: Review if this is the place for default options
        private static IEnumerable<string> GetDefaultOptions()
        {
            var args = new List<string>()
            {
                "--noframework",
                "--nologo",
                "--simpleresolution"
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                args.Add("--debug:full");
            else
                args.Add("--debug-");

            return args;
        }

        private static IEnumerable<string> TranslateCommonOptions(CommonCompilerOptions options, string outputName)
        {
            List<string> commonArgs = new List<string>();

            if (options.Defines != null)
            {
                commonArgs.AddRange(options.Defines.Select(def => $"-d:{def}"));
            }

            if (options.Platform != null)
            {
                commonArgs.Add($"--platform:{options.Platform}");
            }

            if (options.WarningsAsErrors == true)
            {
                commonArgs.Add("--warnaserror");
            }

            if (options.Optimize == true)
            {
                commonArgs.Add("--optimize");
            }

            if(options.GenerateXmlDocumentation == true)
            {
                commonArgs.Add($"--doc:{Path.ChangeExtension(outputName, "xml")}");
            }

            if (options.EmitEntryPoint != true)
            {
                commonArgs.Add("--target:library");
            }
            else
            {
                commonArgs.Add("--target:exe");

                //HACK we need default.win32manifest for exe
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var win32manifestPath = Path.Combine(AppContext.BaseDirectory, "default.win32manifest");
                    commonArgs.Add($"--win32manifest:\"{win32manifestPath}\"");
                }
            }

            return commonArgs;
        }

        private static Command RunFsc(List<string> fscArgs)
        {
            var corerun = Path.Combine(AppContext.BaseDirectory, Constants.HostExecutableName);
            var fscExe = Path.Combine(AppContext.BaseDirectory, "fsc.exe");

            List<string> args = new List<string>();
            args.Add(fscExe);
            args.AddRange(fscArgs);
            
            return Command.Create(corerun, args.ToArray());
        }
    }
}
