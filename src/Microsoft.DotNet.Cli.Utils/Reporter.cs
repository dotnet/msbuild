using System.Runtime.InteropServices;
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
