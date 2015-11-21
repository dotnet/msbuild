using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            return ExecuteApp(args);
        }

        private static ArgValues GetArgs(string[] args)
        {
            string inputAssembly = null;
            string outputDirectory = null;
            string temporaryOutputDirectory = null;
            string configuration = null;
            BuildConfiguration? buildConfiguration = null; 
            string mode = null;
            NativeIntermediateMode? nativeMode = null;
            string ilcArgs = null;
            string ilcPath = null;
            string appDepSdk = null;
            string logPath = null;

            IReadOnlyList<string> references = Array.Empty<string>();
            IReadOnlyList<string> linklib = Array.Empty<string>();

            try
            {                
                ArgumentSyntax.Parse(args, syntax =>
                {                                        
                    syntax.DefineOption("output", ref outputDirectory, "Output Directory for native executable.");
                    syntax.DefineOption("temp-output", ref temporaryOutputDirectory, "Directory for intermediate files.");

                    syntax.DefineOption("configuration", ref configuration, "debug/release build configuration. Defaults to debug.");
                    syntax.DefineOption("mode", ref mode, "Code Generation mode. Defaults to ryujit.");

                    syntax.DefineOptionList("reference", ref references, "Use to specify Managed DLL references of the app.");

                    // Custom Extensibility Points to support CoreRT workflow TODO better descriptions
                    syntax.DefineOption("ilcargs", ref ilcArgs, "Use to specify custom arguments for the IL Compiler.");
                    syntax.DefineOption("ilcpath", ref ilcPath, "Use to plug in a custom built ilc.exe");
                    syntax.DefineOptionList("linklib", ref linklib, "Use to link in additional static libs");

                    // TEMPORARY Hack until CoreRT compatible Framework Libs are available 
                    syntax.DefineOption("appdepsdk", ref appDepSdk, "Use to plug in custom appdepsdk path");

                    // Optional Log Path
                    syntax.DefineOption("logpath", ref logPath, "Use to dump Native Compilation Logs to a file.");

                    syntax.DefineParameter("INPUT_ASSEMBLY", ref inputAssembly, "The managed input assembly to compile to native.");

                    if (string.IsNullOrWhiteSpace(inputAssembly))
                    {
                        syntax.ReportError("Input Assembly is a required parameter.");
                    }

                    if (!string.IsNullOrEmpty(configuration))
                    {
                        try
                        {
                            buildConfiguration = EnumExtensions.Parse<BuildConfiguration>(configuration);
                        }
                        catch (ArgumentException)
                        {
                            syntax.ReportError($"Invalid Configuration Option: {configuration}");
                        }
                    }

                    if (!string.IsNullOrEmpty(mode))
                    {
                        try
                        {
                            nativeMode = EnumExtensions.Parse<NativeIntermediateMode>(mode);
                        }
                        catch (ArgumentException)
                        {
                            syntax.ReportError($"Invalid Mode Option: {mode}");
                        }
                    }
                });
            }
            catch (ArgumentSyntaxException)
            {
                //return ExitFailed;
            }

            Console.WriteLine($"Input Assembly: {inputAssembly}");

            return new ArgValues
            {
                InputManagedAssemblyPath = inputAssembly,
                OutputDirectory = outputDirectory,
                IntermediateDirectory = temporaryOutputDirectory,
                Architecture = ArchitectureMode.x64,
                BuildConfiguration = buildConfiguration,
                NativeMode = nativeMode,
                ReferencePaths = references.ToList(),
                IlcArgs = ilcArgs,
                IlcPath = ilcPath,
                LinkLibPaths = linklib.ToList(),
                AppDepSDKPath = appDepSdk,
                LogPath = logPath
            };
        }

        private static int ExecuteApp(string[] args)
        {   
            // Support Response File
            foreach(var arg in args)
            {
                if(arg.Contains(".rsp"))
                {
                    args = ParseResponseFile(arg);

                    if (args == null)
                    {
                        return 1;
                    }
                }
            }

            try
            {
                var cmdLineArgs = GetArgs(args);
                var config = ParseAndValidateArgs(cmdLineArgs);

                DirectoryExtensions.CleanOrCreateDirectory(config.OutputDirectory);
                DirectoryExtensions.CleanOrCreateDirectory(config.IntermediateDirectory);

                var nativeCompiler = NativeCompiler.Create(config);

                var result = nativeCompiler.CompileToNative(config);

                return result ? 0 : 1;
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex);
#else
                Reporter.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

        private static string[] ParseResponseFile(string rspPath)
        {
            if (!File.Exists(rspPath))
            {
                Reporter.Error.WriteLine("Invalid Response File Path");
                return null;
            }

            string content;
            try
            {
                content = File.ReadAllText(rspPath);
            }
            catch (Exception)
            {
                Reporter.Error.WriteLine("Unable to Read Response File");
                return null;
            }

            var nArgs = content.Split(new [] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
            return nArgs;
        }
        
        private static NativeCompileSettings ParseAndValidateArgs(ArgValues args)
        {
            var config = NativeCompileSettings.Default;

            config.InputManagedAssemblyPath = args.InputManagedAssemblyPath;
            config.Architecture = args.Architecture;

            if (args.BuildConfiguration.HasValue)
            {
                config.BuildType = args.BuildConfiguration.Value;
            }            
            
            if(!string.IsNullOrEmpty(args.OutputDirectory))
            {
                config.OutputDirectory = args.OutputDirectory;
            }
                        
            if(!string.IsNullOrEmpty(args.IntermediateDirectory))
            {
                config.IntermediateDirectory = args.IntermediateDirectory; 
            }
            
            if (args.NativeMode.HasValue)
            {
                config.NativeMode = args.NativeMode.Value;
            }

            if(!string.IsNullOrEmpty(args.AppDepSDKPath))
            {
                config.AppDepSDKPath = args.AppDepSDKPath;
            }

            if (!string.IsNullOrEmpty(args.IlcPath))
            {                
                config.IlcPath = args.IlcPath;
            }

            if (!string.IsNullOrEmpty(args.LogPath))
            {
                config.LogPath = Path.GetFullPath(args.LogPath);
            }

            if (!string.IsNullOrEmpty(args.IlcArgs))
            {
                config.IlcArgs = Path.GetFullPath(args.IlcArgs);
            }

            foreach (var reference in args.ReferencePaths)
            {
                config.AddReference(reference);
            }

            foreach (var lib in args.LinkLibPaths)
            {
                config.AddLinkLibPath(lib);
            }
            
            return config;
        }        
    }
}
