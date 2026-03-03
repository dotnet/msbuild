// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests.Shared
{
    public static class EndToEndTestUtilities
    {
        public static ArtifactsLocationAttribute ArtifactsLocationAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<ArtifactsLocationAttribute>()
                                                   ?? throw new InvalidOperationException("This test assembly does not have the ArtifactsLocationAttribute");

        public static string BootstrapMsBuildBinaryLocation => BootstrapLocationAttribute.BootstrapMsBuildBinaryLocation;

        public static string BootstrapSdkVersion => BootstrapLocationAttribute.BootstrapSdkVersion;

        public static string BootstrapRootPath => BootstrapLocationAttribute.BootstrapRoot;

        public static string LatestDotNetCoreForMSBuild => BootstrapLocationAttribute.LatestDotNetCoreForMSBuild;

        internal static BootstrapLocationAttribute BootstrapLocationAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<BootstrapLocationAttribute>()
                                           ?? throw new InvalidOperationException("This test assembly does not have the BootstrapLocationAttribute");

        public static string ExecBootstrapedMSBuild(
            string msbuildParameters,
            out bool successfulExit,
            bool shellExecute = false,
            ITestOutputHelper outputHelper = null,
            bool attachProcessId = true,
            int timeoutMilliseconds = 30_000)
        {
#if NET
            string pathToExecutable = EnvironmentProvider.GetDotnetExePathFromFolder(BootstrapMsBuildBinaryLocation);
            msbuildParameters = Path.Combine(BootstrapMsBuildBinaryLocation, "sdk", BootstrapLocationAttribute.BootstrapSdkVersion, "MSBuild.dll") + " " + msbuildParameters;
#else
            string pathToExecutable = Path.Combine(BootstrapMsBuildBinaryLocation, "MSBuild.exe");
#endif
            return RunnerUtilities.RunProcessAndGetOutput(pathToExecutable, msbuildParameters, out successfulExit, shellExecute, outputHelper, attachProcessId, timeoutMilliseconds);
        }
    }
}
