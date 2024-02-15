// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.UnitTests.Shared;
using Xunit.Abstractions;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#endif

namespace Microsoft.Build.Analyzers.UnitTests
{
    internal static class BootstrapRunner
    {
        // This should ideally be part of RunnerUtilities - however then we'd need to enforce
        //  all test projects to import the BootStrapMSBuild.props file and declare the BootstrapLocationAttribute.
        // Better solution would be to have a single test utility project - instead of linked code files.
        public static string ExecBootstrapedMSBuild(string msbuildParameters, out bool successfulExit, bool shellExecute = false, ITestOutputHelper? outputHelper = null)
        {
            BootstrapLocationAttribute attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<BootstrapLocationAttribute>()
                ?? throw new InvalidOperationException("This test assembly does not have the BootstrapLocationAttribute");

            string binaryFolder = attribute.BootstrapMsbuildBinaryLocation;
            string? bindirOverride = Environment.GetEnvironmentVariable("MSBUILD_BOOTSTRAPPED_BINDIR");
            if (!string.IsNullOrEmpty(bindirOverride))
            {
                // The bootstrap environment has moved to another location. Assume the same relative layout and adjust the path.
                string relativePath = Path.GetRelativePath(attribute.BootstrapRoot, binaryFolder);
                binaryFolder = Path.GetFullPath(relativePath, bindirOverride);
            }
#if NET
            string pathToExecutable = EnvironmentProvider.GetDotnetExePath()!;
            msbuildParameters = Path.Combine(binaryFolder, "MSBuild.dll") + " " + msbuildParameters;
#else
            string pathToExecutable =
                Path.Combine(binaryFolder, "msbuild.exe");
#endif
            return RunnerUtilities.RunProcessAndGetOutput(pathToExecutable, msbuildParameters, out successfulExit, shellExecute, outputHelper);
        }
    }
}
