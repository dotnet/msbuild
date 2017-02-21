using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli
{
    internal static class ProcessStartInfoExtensions
    {
        public static int Execute(this ProcessStartInfo startInfo)
        {
            if (startInfo == null)
            {
                throw new ArgumentNullException(nameof(startInfo));
            }

            var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();
            process.WaitForExit();

            return process.ExitCode;
        }
    }
}
