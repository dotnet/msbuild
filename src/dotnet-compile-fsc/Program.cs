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
using Microsoft.DotNet.ProjectModel.Resolution;
using NuGet.Frameworks;
using System.Reflection;

namespace Microsoft.DotNet.Tools.Compiler.Fsc
{
    public class CompileFscCommand
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

            // TODO less hacky
            bool targetNetCore = 
                commonOptions.Defines.Contains("DNXCORE50") ||
                commonOptions.Defines.Where(d => d.StartsWith("NETSTANDARDAPP1_")).Any() ||
                commonOptions.Defines.Where(d => d.StartsWith("NETSTANDARD1_")).Any();

            // Get FSC Path upfront to use it for win32manifest path
            var fscCommandSpec = ResolveFsc(null, tempOutDir);
            var fscExeFile = fscCommandSpec.FscExeFile;
            var fscExeDir = fscCommandSpec.FscExeDir;

            // FSC arguments
            var allArgs = new List<string>();

            //HACK fsc raise error FS0208 if target exe doesnt have extension .exe
            bool hackFS0208 = targetNetCore && commonOptions.EmitEntryPoint == true;

            var originalOutputName = outputName;

            if (outputName != null)
            {
                if (hackFS0208)
                {
                    outputName = Path.ChangeExtension(outputName, ".exe");
                }

                allArgs.Add($"--out:{outputName}");
            }

            //let's pass debugging type only if options.DebugType is specified, until 
            //portablepdb are confirmed to work.
            //so it's possibile to test portable pdb without breaking existing build
            if (string.IsNullOrEmpty(commonOptions.DebugType))
            {
                //debug info (only windows pdb supported, not portablepdb)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    allArgs.Add("--debug");
                    //TODO check if full or pdbonly
                    allArgs.Add("--debug:pdbonly");
                }
                else
                    allArgs.Add("--debug-");
            }
            else
            {
                allArgs.Add("--debug");
                allArgs.Add($"--debug:{commonOptions.DebugType}");
            }

            // Default options
            allArgs.Add("--noframework");
            allArgs.Add("--nologo");
            allArgs.Add("--simpleresolution");
            allArgs.Add("--nocopyfsharpcore");

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
                var win32manifestPath = Path.Combine(fscExeDir, "..", "..", "runtimes", "any", "native", "default.win32manifest");
                allArgs.Add($"--win32manifest:{win32manifestPath}");
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
            var result = RunFsc(new List<string> { $"@{rsp}" }, tempOutDir)
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

        private static Command RunFsc(List<string> fscArgs, string temp)
        {
            var fscEnvExe = Environment.GetEnvironmentVariable("DOTNET_FSC_PATH");
            var exec = Environment.GetEnvironmentVariable("DOTNET_FSC_EXEC")?.ToUpper() ?? "COREHOST";
            
            var muxer = new Muxer();

            if (fscEnvExe != null)
            {
                switch (exec)
                {
                    case "RUN":
                        return Command.Create(fscEnvExe, fscArgs.ToArray());

                    case "COREHOST":
                    default:
                        var host = muxer.MuxerPath;
                        return Command.Create(host, new[] { fscEnvExe }.Concat(fscArgs).ToArray());
                }
            }
            else
            {
                var fscCommandSpec =  ResolveFsc(fscArgs, temp)?.Spec;
                return Command.Create(fscCommandSpec);
            }
        }

        private static FscCommandSpec ResolveFsc(List<string> fscArgs, string temp)
        {
            var nugetPackagesRoot = PackageDependencyProvider.ResolvePackagesPath(null, null);
            var depsFile = Path.Combine(AppContext.BaseDirectory, "dotnet-compile-fsc" + FileNameSuffixes.DepsJson);

            var depsJsonCommandResolver = new DepsJsonCommandResolver(nugetPackagesRoot);
            var dependencyContext = depsJsonCommandResolver.LoadDependencyContextFromFile(depsFile);
            var fscPath = depsJsonCommandResolver.GetCommandPathFromDependencyContext("fsc", dependencyContext);


            var commandResolverArgs = new CommandResolverArguments()
            {
                CommandName = "fsc",
                CommandArguments = fscArgs,
                DepsJsonFile = depsFile
            };

            var fscCommandSpec = depsJsonCommandResolver.Resolve(commandResolverArgs);

            var runtimeConfigFile = Path.Combine(
                Path.GetDirectoryName(typeof(CompileFscCommand).GetTypeInfo().Assembly.Location)
                , "dotnet-compile-fsc" + FileNameSuffixes.RuntimeConfigJson);


            CopyRuntimeConfigForFscExe(runtimeConfigFile, "fsc", depsFile, nugetPackagesRoot, fscPath);

            return new FscCommandSpec
            {
                Spec = fscCommandSpec,
                FscExeDir = Path.GetDirectoryName(fscPath),
                FscExeFile = fscPath
            };
        }

        private static void CopyRuntimeConfigForFscExe(
            string runtimeConfigFile,
            string commandName,
            string depsJsonFile,
            string nugetPackagesRoot,
            string fscPath)
        {   
            var newFscRuntimeConfigDir = Path.GetDirectoryName(fscPath);
            var newFscRuntimeConfigFile = Path.Combine(
                newFscRuntimeConfigDir, 
                Path.GetFileNameWithoutExtension(fscPath) + FileNameSuffixes.RuntimeConfigJson);
        
            try
            {
                File.Copy(runtimeConfigFile, newFscRuntimeConfigFile, true);
            }
            catch(Exception e)
            {
                Reporter.Error.WriteLine("Failed to copy fsc runtimeconfig.json");
                throw e;
            }
        }

        private class FscCommandSpec
        {
            public CommandSpec Spec { get; set; }
            public string FscExeDir { get; set; }
            public string FscExeFile { get; set; }
        }
    }
}

