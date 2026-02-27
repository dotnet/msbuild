// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for MSBuild App Host support functionality.
    /// Tests the DOTNET_ROOT environment variable handling for app host bootstrap.
    /// </summary>
    public sealed class AppHostSupport_Tests
    {
        private readonly ITestOutputHelper _output;

        private readonly string _dotnetHostPath = NativeMethodsShared.IsWindows
            ? @"C:\Program Files\dotnet\dotnet.exe"
            : "/usr/share/dotnet/dotnet";

        public AppHostSupport_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CreateDotnetRootEnvironmentOverrides_SetsDotnetRootFromHostPath()
        {
            var overrides = DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides(_dotnetHostPath);

            overrides.ShouldNotBeNull();
            overrides.ShouldContainKey("DOTNET_ROOT");

            string expectedDotnetRoot = Path.GetDirectoryName(_dotnetHostPath);
            overrides["DOTNET_ROOT"].ShouldBe(expectedDotnetRoot);
        }

        [Fact]
        public void CreateDotnetRootEnvironmentOverrides_ClearsArchitectureSpecificVariables()
        {
            var overrides = DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides(_dotnetHostPath);

            // Assert - architecture-specific variables should be set to null (to be cleared)
            overrides.ShouldContainKey("DOTNET_ROOT_X64");
            overrides["DOTNET_ROOT_X64"].ShouldBeNull();

            overrides.ShouldContainKey("DOTNET_ROOT_X86");
            overrides["DOTNET_ROOT_X86"].ShouldBeNull();

            overrides.ShouldContainKey("DOTNET_ROOT_ARM64");
            overrides["DOTNET_ROOT_ARM64"].ShouldBeNull();
        }


        [WindowsOnlyTheory]
        [InlineData(@"C:\custom\sdk\dotnet.exe", @"C:\custom\sdk")]
        [InlineData(@"D:\tools\dotnet\dotnet.exe", @"D:\tools\dotnet")]
        public void CreateDotnetRootEnvironmentOverrides_HandlesVariousPaths_Windows(string hostPath, string expectedRoot)
        {
            var overrides = DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides(hostPath);

            overrides["DOTNET_ROOT"].ShouldBe(expectedRoot);
        }

        [UnixOnlyTheory]
        [InlineData("/usr/local/share/dotnet/dotnet", "/usr/local/share/dotnet")]
        [InlineData("/home/user/.dotnet/dotnet", "/home/user/.dotnet")]
        public void CreateDotnetRootEnvironmentOverrides_HandlesVariousPaths_Unix(string hostPath, string expectedRoot)
        {
            var overrides = DotnetHostEnvironmentHelper.CreateDotnetRootEnvironmentOverrides(hostPath);

            overrides["DOTNET_ROOT"].ShouldBe(expectedRoot);
        }

        [Fact]
        public void ClearBootstrapDotnetRootEnvironment_ClearsVariablesNotInOriginalEnvironment()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                // Arrange - set DOTNET_ROOT variants that simulate app host bootstrap
                env.SetEnvironmentVariable("DOTNET_ROOT", @"C:\TestDotnet");
                env.SetEnvironmentVariable("DOTNET_ROOT_X64", @"C:\TestDotnetX64");

                // Original environment does NOT have these variables
                var originalEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                DotnetHostEnvironmentHelper.ClearBootstrapDotnetRootEnvironment(originalEnvironment);

                Environment.GetEnvironmentVariable("DOTNET_ROOT").ShouldBeNull();
                Environment.GetEnvironmentVariable("DOTNET_ROOT_X64").ShouldBeNull();
            }
        }

        [Fact]
        public void ClearBootstrapDotnetRootEnvironment_PreservesVariablesInOriginalEnvironment()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                // Arrange - set DOTNET_ROOT that was in the original environment
                string originalValue = @"C:\OriginalDotnet";
                env.SetEnvironmentVariable("DOTNET_ROOT", originalValue);

                // Register other DOTNET_ROOT variants with TestEnvironment so cleanup works correctly.
                // These will be cleared by the helper if not in originalEnvironment.
                env.SetEnvironmentVariable("DOTNET_ROOT_X64", null);
                env.SetEnvironmentVariable("DOTNET_ROOT_X86", null);
                env.SetEnvironmentVariable("DOTNET_ROOT_ARM64", null);

                // Original environment HAS DOTNET_ROOT
                var originalEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DOTNET_ROOT"] = originalValue
                };

                DotnetHostEnvironmentHelper.ClearBootstrapDotnetRootEnvironment(originalEnvironment);

                // Assert - DOTNET_ROOT should be preserved since it was in original environment
                Environment.GetEnvironmentVariable("DOTNET_ROOT").ShouldBe(originalValue);
            }
        }

        [Fact]
        public void ClearBootstrapDotnetRootEnvironment_HandlesMixedScenario()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                string originalDotnetRoot = @"C:\OriginalDotnet";
                string bootstrapX64 = @"C:\BootstrapX64";

                env.SetEnvironmentVariable("DOTNET_ROOT", originalDotnetRoot);
                env.SetEnvironmentVariable("DOTNET_ROOT_X64", bootstrapX64);
                env.SetEnvironmentVariable("DOTNET_ROOT_X86", @"C:\BootstrapX86");

                // Original environment has DOTNET_ROOT but not the architecture-specific ones
                var originalEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DOTNET_ROOT"] = originalDotnetRoot
                };

                DotnetHostEnvironmentHelper.ClearBootstrapDotnetRootEnvironment(originalEnvironment);

                Environment.GetEnvironmentVariable("DOTNET_ROOT").ShouldBe(originalDotnetRoot); // Preserved
                Environment.GetEnvironmentVariable("DOTNET_ROOT_X64").ShouldBeNull(); // Cleared
                Environment.GetEnvironmentVariable("DOTNET_ROOT_X86").ShouldBeNull(); // Cleared
                Environment.GetEnvironmentVariable("DOTNET_ROOT_ARM64").ShouldBeNull(); // Was already null
            }
        }
    }
}
