using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Restore
{
    internal static class NuGet3
    {
        public static int Restore(IEnumerable<string> args, bool quiet)
        {
            var prefixArgs = new List<string>();
            if (quiet)
            {
                prefixArgs.Add("--verbosity");
                prefixArgs.Add("Error");
            }
            prefixArgs.Add("restore");

            var result = Run(Enumerable.Concat(
                    prefixArgs,
                    args))
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();

            return result.ExitCode;
        }

        private static Command Run(IEnumerable<string> nugetArgs)
        {
            var corerun = Path.Combine(
                AppContext.BaseDirectory,
                "corerun" + Constants.ExeSuffix);
            return Command.Create(corerun, Enumerable.Concat(
                new[] { Path.Combine(AppContext.BaseDirectory, "NuGet.CommandLine.XPlat.dll") },
                nugetArgs));
        }
    }
}
