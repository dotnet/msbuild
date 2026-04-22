// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Constants = Microsoft.Build.Framework.Constants;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for the process-name resolution used by
    /// <c>NodeProviderOutOfProcBase.GetPossibleRunningNodes</c>. This is the surface area changed
    /// by the fix for <see href="https://github.com/dotnet/msbuild/issues/13508"/>:
    /// <c>ShutdownAllNodes</c> must look for both the host kind that *would* be launched right
    /// now (e.g. <c>dotnet</c>) and the alternate AppHost name (<c>MSBuild</c>), so idle worker
    /// nodes started by either host kind are discovered.
    /// </summary>
    /// <remarks>
    /// A full end-to-end test of <c>ShutdownAllNodes</c> across host kinds is structurally
    /// infeasible from a unit test: worker connect handshakes include the launcher's
    /// <c>MSBuildToolsDirectory32</c>, so workers spawned by the bootstrapped MSBuild won't
    /// answer to a shutdown call originating from the test assembly's MSBuild.dll. We therefore
    /// validate the fix at the resolver layer where the bug actually lived.
    /// </remarks>
    public class NodeProcessNameResolution_Tests
    {
        private const string AppHostName = "MSBuild";  // Constants.MSBuildAppName
        private static readonly string AppHostExeName = Constants.MSBuildExecutableName;

        private static void ShouldContainIgnoreCase(string[] names, string expected) =>
            names.Any(n => string.Equals(n, expected, StringComparison.OrdinalIgnoreCase))
                .ShouldBeTrue($"Expected names [{string.Join(", ", names)}] to contain '{expected}' (case-insensitive).");

        [Fact]
        public void ReuseBranch_AppHostPath_ReturnsAppHostName()
        {
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearchCore(
                msbuildLocation: Path.Combine("c:", "tools", AppHostExeName),
                configuredNodeExeLocation: null);

            names.ShouldBe([AppHostName]);
        }

        [Fact]
        public void ReuseBranch_DllPath_ReturnsHostName()
        {
            // For a managed-assembly path the launcher uses the current host (e.g. "dotnet" on .NET Core).
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearchCore(
                msbuildLocation: Path.Combine("c:", "tools", Constants.MSBuildAssemblyName),
                configuredNodeExeLocation: null);

            names.Length.ShouldBe(1);
            names[0].ShouldNotBeNullOrEmpty();
        }

        /// <summary>
        /// Regression for <see href="https://github.com/dotnet/msbuild/issues/13508"/>:
        /// when shutdown is invoked with no active build (<c>configuredNodeExeLocation == null</c>),
        /// the resolver must include the <c>MSBuild</c> AppHost name so that idle worker nodes
        /// launched as <c>MSBuild.exe</c> are discovered, regardless of the current host kind.
        /// </summary>
        [Fact]
        public void ShutdownBranch_NoConfiguredLocation_AlwaysIncludesAppHostName()
        {
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearchCore(
                msbuildLocation: null,
                configuredNodeExeLocation: null);

            ShouldContainIgnoreCase(names, AppHostName);
        }

        /// <summary>
        /// When the active build's <see cref="Microsoft.Build.Execution.BuildParameters.NodeExeLocation"/>
        /// is the AppHost, the resolver must still include both the AppHost name and the alternate
        /// host name so a defensive shutdown still finds nodes launched by the other host kind.
        /// </summary>
        [Fact]
        public void ShutdownBranch_ConfiguredAppHostLocation_IncludesBothNames()
        {
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearchCore(
                msbuildLocation: null,
                configuredNodeExeLocation: Path.Combine("c:", "tools", AppHostExeName));

            ShouldContainIgnoreCase(names, AppHostName);
#if NET
            // On .NET Core the alternate host is the current host (e.g. "dotnet").
            names.Length.ShouldBe(2);
#endif
        }

#if NET
        /// <summary>
        /// On .NET Core, when the configured location is a managed assembly (DLL), the resolver
        /// returns the current host (e.g. "dotnet") plus the AppHost name as defensive fallback.
        /// </summary>
        [Fact]
        public void ShutdownBranch_NetCore_ConfiguredDllLocation_IncludesAppHostFallback()
        {
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearchCore(
                msbuildLocation: null,
                configuredNodeExeLocation: Path.Combine("c:", "tools", Constants.MSBuildAssemblyName));

            ShouldContainIgnoreCase(names, AppHostName);
            names.Length.ShouldBe(2);
        }
#endif
    }
}
