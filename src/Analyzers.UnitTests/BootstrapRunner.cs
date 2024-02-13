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

namespace Microsoft.Build.Analyzers.UnitTests
{
    internal static class BootstrapRunner
    {
        // This should ideally be part of RunnerUtilities - however then we'd need to enforce
        //  all test projects to import the BootStrapMSBuild.props file and declare the BootstrapLocationAttribute.
        // Better solution would be to have a single test utility project - instead of linked code files.
        public static string ExecBootstrapedMSBuild(string msbuildParameters, out bool successfulExit, bool shellExecute = false, ITestOutputHelper outputHelper = null)
        {
            var binaryFolder = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<BootstrapLocationAttribute>()!
                .BootstrapMsbuildBinaryLocation;

#if NET
            string pathToExecutable = EnvironmentProvider.GetDotnetExePath()!;
            msbuildParameters = Path.Combine(binaryFolder, "msbuild.dll") + " " + msbuildParameters;
#else
            string pathToExecutable =
                Path.Combine(binaryFolder, "msbuild.exe");
#endif
            return RunnerUtilities.RunProcessAndGetOutput(pathToExecutable, msbuildParameters, out successfulExit, shellExecute, outputHelper);
        }
    }
}
