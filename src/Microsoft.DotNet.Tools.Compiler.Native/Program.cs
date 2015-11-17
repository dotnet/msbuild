using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);
            
            var app = SetupApp();
            
            return ExecuteApp(app, args);
        }
        
        private static int ExecuteApp(CommandLineApplication app, string[] args)
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
                return app.Execute(args);
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Console.WriteLine(ex);
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

            string content = null;
            try
            {
                content = File.ReadAllText(rspPath);
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine("Unable to Read Response File");
                return null;
            }

            string[] nArgs = Helpers.SplitStringCommandLine(rspPath).ToArray();
            return nArgs;
        }

        private static CommandLineApplication SetupApp()
        {
            var app = new CommandLineApplication();
            app.Name = "dotnet compile native";
            app.FullName = "IL to Native compiler";
            app.Description = "IL to Native compiler Compiler for the .NET Platform";
            app.HelpOption("-h|--help");

            var managedInputArg = app.Argument("<INPUT_ASSEMBLY>", "The managed input assembly to compile to native.");
            var outputArg = app.Option("-o|--out <OUTPUT_DIR>", "Output Directory for native executable.", CommandOptionType.SingleValue);
            var intermediateArg = app.Option("--temp-output <OUTPUT_DIR>", "Directory for intermediate files.", CommandOptionType.SingleValue);
            var archArg = app.Option("-a|--arch <ARCH>", "Architecture type to compile for, defaults to the arch of the machine.", CommandOptionType.SingleValue );
            var buildTypeArg = app.Option("-c|--configuration <TYPE>", "debug/release build type. Defaults to debug.", CommandOptionType.SingleValue);
            var modeArg = app.Option("-m|--mode <MODE>", "Code Generation mode. Defaults to ryujit. ", CommandOptionType.SingleValue);

            var referencesArg = app.Option("-r|--reference <REF_PATH>", "Use to specify Managed DLL references of the app.", CommandOptionType.MultipleValue);
            
            // Custom Extensibility Points to support CoreRT workflow TODO better descriptions
            var ilcArgs = app.Option("--ilcargs <CODEGEN>", "Use to specify custom arguments for the IL Compiler.", CommandOptionType.SingleValue);
            var iltonativePathArg = app.Option("--iltonative-path <ILTONATIVE>", "Use to plug in a custom built iltonative.exe", CommandOptionType.SingleValue);
            var runtimeLibPathArg = app.Option("--runtimelib-path <LIB_PATH>", "Use to plug in custom runtime and bootstrapper libs.", CommandOptionType.SingleValue);
            var linklibArg = app.Option("--linklib <LINKLIB>", "Use to link in additional static libs", CommandOptionType.MultipleValue);
            
            // TEMPORARY Hack until CoreRT compatible Framework Libs are available 
            var appdepSDKPathArg = app.Option("--appdepsdk <SDK>", "Use to plug in custom appdepsdk path", CommandOptionType.SingleValue);
            
            // Optional Log Path
            var logpathArg = app.Option("--logpath <LOG_PATH>", "Use to dump Native Compilation Logs to a file.", CommandOptionType.SingleValue);

            // Use Response File
            var responseFilePathArg = app.Option("--rsp <RSP_FILE>", "Compilation Response File", CommandOptionType.SingleValue);

            app.OnExecute(() =>
            {
                var cmdLineArgs = new ArgValues()
                {
                    InputManagedAssemblyPath = managedInputArg.Value,
                    OutputDirectory = outputArg.Value(),
                    IntermediateDirectory = intermediateArg.Value(),
                    Architecture = archArg.Value(),
                    BuildType = buildTypeArg.Value(),
                    NativeMode = modeArg.Value(),
                    ReferencePaths = referencesArg.Values,
                    IlcArgs = ilcArgs.Value(),
                    ILToNativePath = iltonativePathArg.Value(),
                    RuntimeLibPath = runtimeLibPathArg.Value(),
                    LinkLibPaths = linklibArg.Values,
                    AppDepSDKPath = appdepSDKPathArg.Value(),
                    LogPath = logpathArg.Value()
                };

                var config = ParseAndValidateArgs(cmdLineArgs);
                
                Helpers.CleanOrCreateDirectory(config.OutputDirectory);
                Helpers.CleanOrCreateDirectory(config.IntermediateDirectory);
                
                var nativeCompiler = NativeCompiler.Create(config);
                
                var result = nativeCompiler.CompileToNative(config);

                return result ? 0 : 1;
            });
            
            return app;
        }
        
        private static Config ParseAndValidateArgs(ArgValues args)
        {
            var config = new Config();
            
            // Managed Input
            if (string.IsNullOrEmpty(args.InputManagedAssemblyPath) || !File.Exists(args.InputManagedAssemblyPath))
            {
                //TODO make this message good
                throw new Exception("Invalid Managed Assembly Argument.");
            }
            
            config.InputManagedAssemblyPath = Path.GetFullPath(args.InputManagedAssemblyPath);
            
            // Architecture
            if(string.IsNullOrEmpty(args.Architecture))
            {
                config.Architecture = Helpers.GetCurrentArchitecture();
            }
            else
            {
                try
                {
                    config.Architecture = Helpers.ParseEnum<ArchitectureMode>(args.Architecture.ToLower());
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid Architecture Option.");
                }
            }
            
            // BuildType 
            if(string.IsNullOrEmpty(args.BuildType))
            {
                config.BuildType = GetDefaultBuildType();
            }
            else
            {
                try
                {
                    config.BuildType = Helpers.ParseEnum<BuildConfiguration>(args.BuildType.ToLower());
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid BuildType Option.");
                }
            }
            
            // Output
            if(string.IsNullOrEmpty(args.OutputDirectory))
            {
                config.OutputDirectory = GetDefaultOutputDir(config);
            }
            else
            {
                config.OutputDirectory = args.OutputDirectory;
            }
            
            // Intermediate
            if(string.IsNullOrEmpty(args.IntermediateDirectory))
            {
                config.IntermediateDirectory = GetDefaultIntermediateDir(config);
            }
            else
            {
                config.IntermediateDirectory = args.IntermediateDirectory; 
            }
            
            // Mode
            if (string.IsNullOrEmpty(args.NativeMode))
            {
                config.NativeMode = GetDefaultNativeMode();
            }
            else
            {
                try
                {
                    config.NativeMode = Helpers.ParseEnum<NativeIntermediateMode>(args.NativeMode.ToLower());
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid Mode Option.");
                }
            }

            // AppDeps (TEMP)
            if(!string.IsNullOrEmpty(args.AppDepSDKPath))
            {
                if (!Directory.Exists(args.AppDepSDKPath))
                {
                    throw new Exception("AppDepSDK Directory does not exist.");
                }

                config.AppDepSDKPath = args.AppDepSDKPath;

                var reference = Path.Combine(config.AppDepSDKPath, "*.dll");
                config.ReferencePaths.Add(reference);
            }
            else
            {
                config.AppDepSDKPath = GetDefaultAppDepSDKPath();

                var reference = Path.Combine(config.AppDepSDKPath, "*.dll");
                config.ReferencePaths.Add(reference);
            }

            // ILToNativePath
            if (!string.IsNullOrEmpty(args.ILToNativePath))
            {
                if (!Directory.Exists(args.ILToNativePath))
                {
                    throw new Exception("ILToNative Directory does not exist.");
                }

                config.ILToNativePath = args.ILToNativePath;
            }
            else
            {
                config.ILToNativePath = GetDefaultILToNativePath();
            }

            // RuntimeLibPath
            if (!string.IsNullOrEmpty(args.RuntimeLibPath))
            {
                if (!Directory.Exists(args.RuntimeLibPath))
                {
                    throw new Exception("RuntimeLib Directory does not exist.");
                }

                config.RuntimeLibPath = args.RuntimeLibPath;
            }
            else
            {
                config.RuntimeLibPath = GetDefaultRuntimeLibPath();
            }

            // logpath
            if (!string.IsNullOrEmpty(args.LogPath))
            {
                config.LogPath = Path.GetFullPath(args.LogPath);
            }

            // CodeGenPath
            if (!string.IsNullOrEmpty(args.IlcArgs))
            {
                config.IlcArgs = Path.GetFullPath(args.IlcArgs);
            }

            // Reference Paths
            foreach (var reference in args.ReferencePaths)
            {
                config.ReferencePaths.Add(Path.GetFullPath(reference));
            }

            // Link Libs
            foreach (var lib in args.LinkLibPaths)
            {
                config.LinkLibPaths.Add(Path.GetFullPath(lib));
            }

            // OS
            config.OS = Helpers.GetCurrentOS();
            
            return config;
        }

        private static string GetDefaultOutputDir(Config config)
        {
            var dir = Path.Combine(Constants.BinDirectoryName, config.Architecture.ToString(), config.BuildType.ToString(), "native");

            return Path.GetFullPath(dir);
        }

        private static string GetDefaultIntermediateDir(Config config)
        {
            var dir = Path.Combine(Constants.ObjDirectoryName, config.Architecture.ToString(), config.BuildType.ToString(), "native");

            return Path.GetFullPath(dir);
        }

        private static BuildConfiguration GetDefaultBuildType()
        {
            return BuildConfiguration.debug;
        }

        private static NativeIntermediateMode GetDefaultNativeMode()
        {
            return NativeIntermediateMode.ryujit;
        }

        private static string GetDefaultAppDepSDKPath()
        {
            var appRoot = AppContext.BaseDirectory;

            var dir = Path.Combine(appRoot, "appdepsdk");

            return dir;
        }

        private static string GetDefaultILToNativePath()
        {
            return AppContext.BaseDirectory;
        }

        private static string GetDefaultRuntimeLibPath()
        {
            return AppContext.BaseDirectory;
        }
    }
}
