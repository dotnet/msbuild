using System;
using System.CommandLine;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.DotNet.Tools.Compiler.Csc
{
    public class Program
    {
        private const int ExitFailed = 1;

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            CommonCompilerOptions commonOptions = null;
            string tempOutDir = null;
            IReadOnlyList<string> references = Array.Empty<string>();
            IReadOnlyList<string> resources = Array.Empty<string>();
            IReadOnlyList<string> sources = Array.Empty<string>();
            string outputName = null;

            try
            {
                ArgumentSyntax.Parse(args, syntax =>
                {
                    commonOptions = CommonCompilerOptionsExtensions.Parse(syntax);

                    syntax.DefineOption("temp-output", ref tempOutDir, "Compilation temporary directory");

                    syntax.DefineOption("out", ref outputName, "Name of the output assembly");

                    syntax.DefineOptionList("r|reference", ref references, "Path to a compiler metadata reference");

                    syntax.DefineOptionList("resource", ref resources, "Resources to embed");

                    syntax.DefineParameterList("source-files", ref sources, "Compilation sources");

                    if (tempOutDir == null)
                    {
                        syntax.ReportError("Option '--temp-output' is required");
                    }
                });
            }
            catch (ArgumentSyntaxException)
            {
                return ExitFailed;
            }

            var translated = TranslateCommonOptions(commonOptions);

            var allArgs = new List<string>(translated);
            allArgs.AddRange(GetDefaultOptions());

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

            // TODO: Move mono args to mcs compiler
            args.Add("-nowarn:CS1701");
            args.Add("-nowarn:CS1702");
            args.Add("-nowarn:CS1705");

            return args;
        }

        private static IEnumerable<string> TranslateCommonOptions(CommonCompilerOptions options)
        {
            List<string> commonArgs = new List<string>();

            if (options.Defines != null)
            {
                commonArgs.AddRange(options.Defines.Select(def => $"-d:{def}"));
            }

            if (options.LanguageVersion != null)
            {
                commonArgs.Add($"-langversion:{options.LanguageVersion}");
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
            }

            if (options.DelaySign == true)
            {
                commonArgs.Add("-delaysign");
            }

            // TODO: What is this? What does it mean to sign without a key?
            // Is this "OSS" signing?
            // if (options.StrongName)

            if (options.EmitEntryPoint != true)
            {
                commonArgs.Add("-t:library");
            }

            return commonArgs;
        }

        private static Command RunCsc(string cscArgs)
        {
            // Locate CoreRun
            string hostRoot = Environment.GetEnvironmentVariable("DOTNET_CSC_PATH");
            if (string.IsNullOrEmpty(hostRoot))
            {
                hostRoot = AppContext.BaseDirectory;
            }
            var corerun = Path.Combine(hostRoot, Constants.HostExecutableName);
            var cscExe = Path.Combine(hostRoot, "csc.exe");
            return File.Exists(corerun) && File.Exists(cscExe)
                ? Command.Create(corerun, $@"""{cscExe}"" {cscArgs}")
                : Command.Create("csc", cscArgs);
        }
    }
}
