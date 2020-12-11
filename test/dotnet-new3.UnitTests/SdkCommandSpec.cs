// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework.Commands
{
    public class SdkCommandSpec
    {
        public string FileName { get; set; }
        public List<string> Arguments { get; set; } = new List<string>();

        public Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

        public List<string> EnvironmentToRemove { get; } = new List<string>();

        public string WorkingDirectory { get; set; }

        private string EscapeArgs()
        {
            //  Note: this doesn't handle invoking .cmd files via "cmd /c" on Windows, which probably won't be necessary here
            //  If it is, refer to the code in WindowsExePreferredCommandSpecFactory in Microsoft.DotNet.Cli.Utils
            return ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(Arguments);
        }

        public Command ToCommand()
        {
            var process = new Process()
            {
                StartInfo = ToProcessStartInfo()
            };
            var ret = new Command(process, trimtrailingNewlines: true);
            return ret;
        }

        public ProcessStartInfo ToProcessStartInfo()
        {
            var ret = new ProcessStartInfo();
            ret.FileName = FileName;
            ret.Arguments = EscapeArgs();
            ret.UseShellExecute = false;
            foreach (var kvp in Environment)
            {
                ret.Environment[kvp.Key] = kvp.Value;
            }
            foreach (var envToRemove in EnvironmentToRemove)
            {
                ret.Environment.Remove(envToRemove);
            }

            if (WorkingDirectory != null)
            {
                ret.WorkingDirectory = WorkingDirectory;
            }

            return ret;
        }
    }
}
