using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using NugetProgram = NuGet.CommandLine.XPlat.Program;

namespace Microsoft.DotNet.Tools.Restore
{
    internal static class NuGet3
    {
        public static int Restore(IEnumerable<string> args)
        {
            var prefixArgs = new List<string>();
            if (!args.Any(s => s.Equals("--verbosity", StringComparison.OrdinalIgnoreCase) || s.Equals("-v", StringComparison.OrdinalIgnoreCase)))
            {
                prefixArgs.Add("--verbosity");
                prefixArgs.Add("minimal");
            }
            prefixArgs.Add("restore");

            var result = Run(Enumerable.Concat(
                    prefixArgs,
                    args).ToArray());

            return result;
        }

        private static int Run(string[] nugetArgs)
        {
            var nugetAsm = typeof(NugetProgram).GetTypeInfo().Assembly;
            var mainMethod = nugetAsm.EntryPoint;
            return (int)mainMethod.Invoke(null, new object[] { nugetArgs });
        }
    }
}
