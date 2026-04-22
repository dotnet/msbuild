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
    /// Tests for <see cref="NodeProviderOutOfProcBase.ResolveProcessNamesToSearch"/>, the resolver
    /// changed by the fix for https://github.com/dotnet/msbuild/issues/13508.
    /// </summary>
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
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearch(
                msbuildLocation: Path.Combine("c:", "tools", AppHostExeName),
                configuredNodeExeLocation: null);

            names.ShouldBe([AppHostName]);
        }

        [Fact]
        public void ReuseBranch_DllPath_ReturnsHostName()
        {
            // For a managed-assembly path the launcher uses the current host (e.g. "dotnet" on .NET Core).
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearch(
                msbuildLocation: Path.Combine("c:", "tools", Constants.MSBuildAssemblyName),
                configuredNodeExeLocation: null);

            names.Length.ShouldBe(1);
            names[0].ShouldNotBeNullOrEmpty();
        }

        // Regression for https://github.com/dotnet/msbuild/issues/13508.
        [Fact]
        public void ShutdownBranch_NoConfiguredLocation_AlwaysIncludesAppHostName()
        {
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearch(
                msbuildLocation: null,
                configuredNodeExeLocation: null);

            ShouldContainIgnoreCase(names, AppHostName);
        }

        [Fact]
        public void ShutdownBranch_ConfiguredAppHostLocation_IncludesBothNames()
        {
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearch(
                msbuildLocation: null,
                configuredNodeExeLocation: Path.Combine("c:", "tools", AppHostExeName));

            ShouldContainIgnoreCase(names, AppHostName);
#if NET
            // On .NET Core the alternate host is the current host (e.g. "dotnet").
            names.Length.ShouldBe(2);
#endif
        }

#if NET
        [Fact]
        public void ShutdownBranch_NetCore_ConfiguredDllLocation_IncludesAppHostFallback()
        {
            string[] names = NodeProviderOutOfProcBase.ResolveProcessNamesToSearch(
                msbuildLocation: null,
                configuredNodeExeLocation: Path.Combine("c:", "tools", Constants.MSBuildAssemblyName));

            ShouldContainIgnoreCase(names, AppHostName);
            names.Length.ShouldBe(2);
        }
#endif
    }
}
