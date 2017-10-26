using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public class SdkCommandSpec
    {
        public string FileName { get; set; }
        public List<string> Arguments { get; set; } = new List<string>();

        public Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

        public string WorkingDirectory { get; set; }

        private string EscapeArgs()
        {
            //  Note: this doesn't handle invoking .cmd files via "cmd /c" on Windows, which probably won't be necessary here
            //  If it is, refer to the code in WindowsExePreferredCommandSpecFactory in Microsoft.DotNet.Cli.Utils
            return ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(Arguments);
        }

        public ICommand ToCommand()
        {
            var commandSpec = new CommandSpec(FileName, EscapeArgs(), CommandResolutionStrategy.Path);
            ICommand ret = Command.Create(commandSpec);
            if (WorkingDirectory != null)
            {
                ret = ret.WorkingDirectory(WorkingDirectory);
            }

            //  It's necessary to set the environment variables here instead of passing them to the CommandSpec constructor,
            //  because if they are passed to the CommandSpec constructor, they won't override existing environment variables,
            //  which can cause the wrong MSBuildSDKsPath to be used
            foreach (var kvp in Environment)
            {
                ret.EnvironmentVariable(kvp.Key, kvp.Value);
            }

            return ret;
        }

        public ProcessStartInfo ToProcessStartInfo()
        {
            var ret = new ProcessStartInfo();
            ret.FileName = FileName;
            ret.Arguments = EscapeArgs();
            foreach (var kvp in Environment)
            {
                ret.Environment[kvp.Key] = kvp.Value;
            }

            if (WorkingDirectory != null)
            {
                ret.WorkingDirectory = WorkingDirectory;
            }

            return ret;
        }
    }


}
