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
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Compiler.Fsc
{
    public class CompileFscCommand
    {
        private const int ExitFailed = 1;

        public static int Run(string[] args)
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


            // TODO less hacky
            bool targetNetCore = 
                commonOptions.Defines.Contains("DNXCORE50") ||
                commonOptions.Defines.Where(d => d.StartsWith("NETSTANDARDAPP1_")).Any() ||
                commonOptions.Defines.Where(d => d.StartsWith("NETSTANDARD1_")).Any();

            // FSC arguments
            var allArgs = new List<string>();

            //HACK fsc raise error FS0208 if target exe doesnt have extension .exe
            bool hackFS0208 = targetNetCore && commonOptions.EmitEntryPoint == true;
            string originalOutputName = outputName;

            if (outputName != null)
            {
                if (hackFS0208)
                {
                    outputName = Path.ChangeExtension(outputName, ".exe");
                }

                allArgs.Add($"--out:{outputName}");
            }

            //debug info (only windows pdb supported, not portablepdb)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                allArgs.Add("--debug");
                //TODO check if full or pdbonly
                allArgs.Add("--debug:pdbonly");
            }
            else
                allArgs.Add("--debug-");

            // Default options
            allArgs.Add("--noframework");
            allArgs.Add("--nologo");
            allArgs.Add("--simpleresolution");

            // project.json compilationOptions
            if (commonOptions.Defines != null)
            {
                allArgs.AddRange(commonOptions.Defines.Select(def => $"--define:{def}"));
            }

            if (commonOptions.GenerateXmlDocumentation == true)
            {
                allArgs.Add($"--doc:{Path.ChangeExtension(outputName, "xml")}");
            }

            if (commonOptions.KeyFile != null)
            {
                allArgs.Add($"--keyfile:{commonOptions.KeyFile}");
            }

            if (commonOptions.Optimize == true)
            {
                allArgs.Add("--optimize+");
            }

            //--resource doesnt expect "
            //bad: --resource:"path/to/file",name 
            //ok:  --resource:path/to/file,name 
            allArgs.AddRange(resources.Select(resource => $"--resource:{resource.Replace("\"", "")}"));

            allArgs.AddRange(references.Select(r => $"-r:{r}"));

            if (commonOptions.EmitEntryPoint != true)
            {
                allArgs.Add("--target:library");
            }
            else
            {
                allArgs.Add("--target:exe");

                //HACK we need default.win32manifest for exe
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var win32manifestPath = Path.Combine(AppContext.BaseDirectory, "default.win32manifest");
                    allArgs.Add($"--win32manifest:{win32manifestPath}");
                }
            }

            if (commonOptions.SuppressWarnings != null)
            {
                allArgs.Add("--nowarn:" + string.Join(",", commonOptions.SuppressWarnings.ToArray()));
            }

            if (commonOptions.LanguageVersion != null)
            {
                // Not used in fsc
            }

            if (commonOptions.Platform != null)
            {
                allArgs.Add($"--platform:{commonOptions.Platform}");
            }

            if (commonOptions.AllowUnsafe == true)
            {
            }

            if (commonOptions.WarningsAsErrors == true)
            {
                allArgs.Add("--warnaserror");
            }

            //set target framework
            if (targetNetCore)
            {
                allArgs.Add("--targetprofile:netcore");
            }

            if (commonOptions.DelaySign == true)
            {
                allArgs.Add("--delaysign+");
            }

            if (commonOptions.PublicSign == true)
            {
            }

            if (commonOptions.AdditionalArguments != null)
            {
                // Additional arguments are added verbatim
                allArgs.AddRange(commonOptions.AdditionalArguments);
            }

            // Generate assembly info
            var assemblyInfo = Path.Combine(tempOutDir, $"dotnet-compile.assemblyinfo.fs");
            File.WriteAllText(assemblyInfo, AssemblyInfoFileGenerator.GenerateFSharp(assemblyInfoOptions));

            //source files + assemblyInfo
            allArgs.AddRange(GetSourceFiles(sources, assemblyInfo).ToArray());

            //TODO check the switch enabled in fsproj in RELEASE and DEBUG configuration 

            var rsp = Path.Combine(tempOutDir, "dotnet-compile-fsc.rsp");
            File.WriteAllLines(rsp, allArgs, Encoding.UTF8);

            // Execute FSC!
            var result = RunFsc(new List<string> { $"@{rsp}" })
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            bool successFsc = result.ExitCode == 0;

            if (hackFS0208 && File.Exists(outputName))
            {
                if (File.Exists(originalOutputName))
                    File.Delete(originalOutputName);
                File.Move(outputName, originalOutputName);
            }

            //HACK dotnet build require a pdb (crash without), fsc atm cant generate a portable pdb, so an empty pdb is created
            string pdbPath = Path.ChangeExtension(outputName, ".pdb");
            if (successFsc && !File.Exists(pdbPath))
            {
                File.WriteAllBytes(pdbPath, Array.Empty<byte>());
            }

            return result.ExitCode;
        }

        private static Command RunFsc(List<string> fscArgs)
        {
            var fscExe = Environment.GetEnvironmentVariable("DOTNET_FSC_PATH")
                      ?? Path.Combine(AppContext.BaseDirectory, "fsc.exe");

            var exec = Environment.GetEnvironmentVariable("DOTNET_FSC_EXEC")?.ToUpper() ?? "COREHOST";

            switch (exec)
            {
                case "RUN":
                    return Command.Create(fscExe, fscArgs.ToArray());

                case "COREHOST":
                default:
                    var corehost = Path.Combine(AppContext.BaseDirectory, Constants.HostExecutableName);
                    return Command.Create(corehost, new[] { fscExe }.Concat(fscArgs).ToArray());
            }

        }

        // The assembly info must be in the last minus 1 position because:
        // - assemblyInfo should be in the end to override attributes
        // - assemblyInfo cannot be in the last position, because last file contains the main
        private static IEnumerable<string> GetSourceFiles(IReadOnlyList<string> sourceFiles, string assemblyInfo)
        {
            if (!sourceFiles.Any())
            {
                yield return assemblyInfo;
                yield break;
            }

            foreach (var s in sourceFiles.Take(sourceFiles.Count() - 1))
                yield return s;

            yield return assemblyInfo;

            yield return sourceFiles.Last();
        }
    }
}
