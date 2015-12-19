using System;
using System.Collections.Generic;
using System.CommandLine;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    internal static class ArgumentsParser
    {
        internal static ArgValues Parse(IEnumerable<string> args)
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
            string ilcSdkPath = null;
            string appDepSdk = null;
            string logPath = null;
            var help = false;
            string helpText = null;
            var returnCode = 0;

            IReadOnlyList<string> references = Array.Empty<string>();
            IReadOnlyList<string> linklib = Array.Empty<string>();            

            try
            {
                ArgumentSyntax.Parse(args, syntax =>
                {
                    syntax.HandleHelp = false;
                    syntax.HandleErrors = false;

                    syntax.DefineOption("output", ref outputDirectory, "Output Directory for native executable.");
                    syntax.DefineOption("temp-output", ref temporaryOutputDirectory, "Directory for intermediate files.");

                    syntax.DefineOption("configuration", ref configuration,
                        "debug/release build configuration. Defaults to debug.");
                    syntax.DefineOption("mode", ref mode, "Code Generation mode. Defaults to ryujit.");

                    syntax.DefineOptionList("reference", ref references,
                        "Use to specify Managed DLL references of the app.");

                    // Custom Extensibility Points to support CoreRT workflow TODO better descriptions
                    syntax.DefineOption("ilcargs", ref ilcArgs, "Use to specify custom arguments for the IL Compiler.");
                    syntax.DefineOption("ilcpath", ref ilcPath, "Use to specify a custom build of IL Compiler.");
                    syntax.DefineOption("ilcsdkpath", ref ilcSdkPath, "Use to specify a custom build of IL Compiler SDK");

                    syntax.DefineOptionList("linklib", ref linklib, "Use to link in additional static libs");

                    // TEMPORARY Hack until CoreRT compatible Framework Libs are available 
                    syntax.DefineOption("appdepsdk", ref appDepSdk, "Use to plug in custom appdepsdk path");

                    // Optional Log Path
                    syntax.DefineOption("logpath", ref logPath, "Use to dump Native Compilation Logs to a file.");

                    syntax.DefineOption("h|help", ref help, "Help for compile native.");

                    syntax.DefineParameter("INPUT_ASSEMBLY", ref inputAssembly,
                        "The managed input assembly to compile to native.");

                    helpText = syntax.GetHelpText();
                    
                    if (string.IsNullOrWhiteSpace(inputAssembly))
                    {
                        syntax.ReportError("Input Assembly is a required parameter.");
                        help = true;
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
                            help = true;
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
                            help = true;
                        }
                    }
                });
            }
            catch (ArgumentSyntaxException exception)
            {
                Console.Error.WriteLine(exception.Message);
                help = true;
                returnCode = 1;
            }

            if (help)
            {
                Console.WriteLine(helpText);

                return new ArgValues
                {
                    IsHelp = help,
                    ReturnCode = returnCode
                };
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
                ReferencePaths = references,
                IlcArgs = ilcArgs,
                IlcPath = ilcPath,
                IlcSdkPath = ilcSdkPath,
                LinkLibPaths = linklib,
                AppDepSDKPath = appDepSdk,
                LogPath = logPath
            };
        }
    }
}
