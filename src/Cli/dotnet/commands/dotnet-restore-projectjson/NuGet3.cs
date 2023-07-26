// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.RestoreProjectJson
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

            var nugetApp = new NuGetForwardingApp(Enumerable.Concat(prefixArgs, args));

            // setting NUGET_XPROJ_WRITE_TARGETS will tell nuget restore to install .props and .targets files
            // coming from NuGet packages
            const string nugetXProjWriteTargets = "NUGET_XPROJ_WRITE_TARGETS";
            bool setXProjWriteTargets = Environment.GetEnvironmentVariable(nugetXProjWriteTargets) == null;
            if (setXProjWriteTargets)
            {
                nugetApp.WithEnvironmentVariable(nugetXProjWriteTargets, "true");
            }

            return nugetApp.Execute();
        }
    }
}
