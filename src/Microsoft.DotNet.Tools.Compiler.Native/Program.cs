using System;
using System.IO;
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
                var cmdLineArgs = ArgumentsParser.Parse(args);
                var config = cmdLineArgs.GetNativeCompileSettings();

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
    }
}
