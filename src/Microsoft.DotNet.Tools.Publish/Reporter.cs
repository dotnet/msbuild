using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.DotNet.Cli.Utils
{
    // Stupid-simple console manager
    internal static class Reporter
    {
        public static AnsiConsole Output { get; } = AnsiConsole.GetOutput(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        public static AnsiConsole Error { get; } = AnsiConsole.GetError(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }
}
