using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    class WindowsCommon
    {
        internal static int SetVCVars()
        {
            // TODO: This is not working because it sets the environment variables in a child process
            // For now get around this by using x86_amd64 cross tools

            // var commonToolsPath = Environment.GetEnvironmentVariable("VS140COMNTOOLS");

            // var scriptPath = Path.Combine(commonToolsPath, "..\\..\\VC\\vcvarsall.bat");
            // var scriptArgs = "x86_amd64";

            // var result = Command.Create(scriptPath, scriptArgs)
            //     .ForwardStdErr()
            //     .ForwardStdOut()
            //     .Execute();

            // return result.ExitCode;
            return 0;
        }
    }
}
