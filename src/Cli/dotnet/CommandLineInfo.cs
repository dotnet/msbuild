// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;
using RuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;
using WorkloadCommandParser = Microsoft.DotNet.Cli.WorkloadCommandParser;

namespace Microsoft.DotNet.Cli
{
    public class CommandLineInfo
    {
        public static void PrintVersion()
        {
            Reporter.Output.WriteLine(Product.Version);
        }

        public static void PrintInfo()
        {
            DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
            var commitSha = versionFile.CommitSha ?? "N/A";
            Reporter.Output.WriteLine($"{LocalizableStrings.DotNetSdkInfoLabel}");
            Reporter.Output.WriteLine($" Version:   {Product.Version}");
            Reporter.Output.WriteLine($" Commit:    {commitSha}");
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine($"{LocalizableStrings.DotNetRuntimeInfoLabel}");
            Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
            Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
            Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
            Reporter.Output.WriteLine($" RID:         {GetDisplayRid(versionFile)}");
            Reporter.Output.WriteLine($" Base Path:   {AppContext.BaseDirectory}");
            PrintWorkloadsInfo();

        }

        private static void PrintWorkloadsInfo()
        {
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine($"{LocalizableStrings.DotnetWorkloadInfoLabel}");
            WorkloadCommandParser.ShowWorkloadsInfo();
        }

        private static string GetDisplayRid(DotnetVersionFile versionFile)
        {
            FrameworkDependencyFile fxDepsFile = new FrameworkDependencyFile();

            string currentRid = RuntimeInformation.RuntimeIdentifier;

            // if the current RID isn't supported by the shared framework, display the RID the CLI was
            // built with instead, so the user knows which RID they should put in their "runtimes" section.
            return fxDepsFile.IsRuntimeSupported(currentRid) ?
                currentRid :
                versionFile.BuildRid;
        }
    }
}
