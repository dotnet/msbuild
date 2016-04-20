using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    internal static class ArgumentsParser
    {
        internal static ArgValues Parse(IEnumerable<string> args)
        {
            CommandLineApplication app = new CommandLineApplication();
            app.HelpOption("-h|--help");

            CommandOption output = app.Option("--output <arg>", "Output Directory for native executable.", CommandOptionType.SingleValue);
            CommandOption tempOutput = app.Option("--temp-output <arg>", "Directory for intermediate files.", CommandOptionType.SingleValue);

            CommandOption configuration = app.Option("--configuration <arg>", "debug/release build configuration. Defaults to debug.", CommandOptionType.SingleValue);
            CommandOption mode = app.Option("--mode <arg>", "Code Generation mode. Defaults to ryujit.", CommandOptionType.SingleValue);

            CommandOption reference = app.Option("--reference <arg>...", "Use to specify Managed DLL references of the app.", CommandOptionType.MultipleValue);

            // Custom Extensibility Points to support CoreRT workflow TODO better descriptions
            CommandOption ilcarg = app.Option("--ilcarg <arg>...", "Use to specify custom arguments for the IL Compiler.", CommandOptionType.MultipleValue);
            CommandOption ilcpath = app.Option("--ilcpath <arg>", "Use to specify a custom build of IL Compiler.", CommandOptionType.SingleValue);
            CommandOption ilcsdkpath = app.Option("ilcsdkpath <arg>", "Use to specify a custom build of IL Compiler SDK", CommandOptionType.SingleValue);

            CommandOption linklib = app.Option("--linklib <arg>...", "Use to link in additional static libs", CommandOptionType.MultipleValue);

            // TEMPORARY Hack until CoreRT compatible Framework Libs are available 
            CommandOption appdepsdk = app.Option("--appdepsdk <arg>", "Use to plug in custom appdepsdk path", CommandOptionType.SingleValue);

            // Optional Log Path
            CommandOption logpath = app.Option("--logpath <arg>", "Use to dump Native Compilation Logs to a file.", CommandOptionType.SingleValue);

            // Optional flags to be passed to the native compiler
            CommandOption cppcompilerflags = app.Option("--cppcompilerflags <arg>", "Additional flags to be passed to the native compiler.", CommandOptionType.SingleValue);

            CommandArgument inputAssembly = app.Argument("INPUT_ASSEMBLY", "The managed input assembly to compile to native.");

            ArgValues argValues = new ArgValues();
            app.OnExecute(() =>
            {
                if (string.IsNullOrEmpty(inputAssembly.Value))
                {
                    Reporter.Error.WriteLine("Input Assembly is a required parameter.");
                    return 1;
                }

                if (configuration.HasValue())
                {
                    try
                    {
                        argValues.BuildConfiguration = EnumExtensions.Parse<BuildConfiguration>(configuration.Value());
                    }
                    catch (ArgumentException)
                    {
                        Reporter.Error.WriteLine($"Invalid Configuration Option: {configuration}");
                        return 1;
                    }
                }

                if (mode.HasValue())
                {
                    try
                    {
                        argValues.NativeMode = EnumExtensions.Parse<NativeIntermediateMode>(mode.Value());
                    }
                    catch (ArgumentException)
                    {
                        Reporter.Error.WriteLine($"Invalid Mode Option: {mode}");
                        return 1;
                    }
                }

                argValues.InputManagedAssemblyPath = inputAssembly.Value;
                argValues.OutputDirectory = output.Value();
                argValues.IntermediateDirectory = tempOutput.Value();
                argValues.Architecture = ArchitectureMode.x64;
                argValues.ReferencePaths = reference.Values;
                argValues.IlcArgs = ilcarg.Values.Select(s =>
                {
                    if (!s.StartsWith("\"") || !s.EndsWith("\""))
                    {
                        throw new ArgumentException("--ilcarg must be specified in double quotes", "ilcarg");
                    }
                    return s.Substring(1, s.Length - 2);
                });
                argValues.IlcPath = ilcpath.Value();
                argValues.IlcSdkPath = ilcsdkpath.Value();
                argValues.LinkLibPaths = linklib.Values;
                argValues.AppDepSDKPath = appdepsdk.Value();
                argValues.LogPath = logpath.Value();
                argValues.CppCompilerFlags = cppcompilerflags.Value();

                Reporter.Output.WriteLine($"Input Assembly: {inputAssembly}");

                return 0;
            });

            try
            {
                argValues.ReturnCode = app.Execute(args.ToArray());
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                argValues.ReturnCode = 1;
            }

            if (argValues.ReturnCode != 0)
            {
                argValues.IsHelp = true;
            }

            return argValues;
        }
    }
}
