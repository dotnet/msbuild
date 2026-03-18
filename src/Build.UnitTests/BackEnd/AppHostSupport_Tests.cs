// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
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

        /// <summary>
        /// Verifies that the handshake toolsDirectory used by the parent
        /// (ResolveAppHostOrFallback) matches what the child process
        /// (NodeEndpointOutOfProcTaskHost) computes via its GetHandshake().
        ///
        /// Regression test for the macOS /tmp → /private/tmp symlink issue
        /// where the parent used $(NetCoreSdkRoot) from MSBuild properties
        /// (unresolved symlink) while the child used MSBuildToolsDirectoryRoot
        /// from AppContext.BaseDirectory (resolved symlink), causing a
        /// handshake hash mismatch → MSB4216.
        /// </summary>
        [Fact]
        public void ResolveAppHostOrFallback_HandshakeMatchesChildHandshake()
        {
            // The parent's handshake should use MSBuildToolsDirectoryRoot —
            // the same source the child (NodeEndpointOutOfProcTaskHost) defaults to.
            string parentToolsDir = BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot;

            // Simulate what the child does in NodeEndpointOutOfProcTaskHost.GetHandshake():
            // new Handshake(options) with no explicit toolsDirectory → defaults to MSBuildToolsDirectoryRoot.
            HandshakeOptions childOptions = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: true,
                taskHostParameters: TaskHostParameters.Empty,
                nodeReuse: false);

            var childHandshake = new Handshake(childOptions);

            // Simulate what the parent does in ResolveAppHostOrFallback (after the fix):
            // new Handshake(hostContext, toolsDirectory: MSBuildToolsDirectoryRoot)
            var parentHandshake = new Handshake(childOptions, parentToolsDir);

            // The handshake keys must match for the pipe connection to succeed.
            parentHandshake.GetKey().ShouldBe(childHandshake.GetKey(),
                "Parent and child handshake keys must match. A mismatch here causes MSB4216 " +
                "because the parent and child listen/connect on different pipe names. " +
                "On macOS, this can happen when symlinks (like /tmp → /private/tmp) cause " +
                "the toolsDirectory to have different string representations.");
        }

        /// <summary>
        /// Proves that using a symlinked path vs a resolved path in the handshake
        /// produces DIFFERENT keys — demonstrating the exact bug on macOS where
        /// /tmp is a symlink to /private/tmp.
        ///
        /// This test creates a real symlink to prove the mismatch. It only runs on
        /// Unix (.NET Core) where symlinks are natively supported and the scenario is relevant.
        /// </summary>
#if NET
        [UnixOnlyFact]
        public void Handshake_WithSymlinkedToolsDirectory_ProducesDifferentKey()
        {
            // Create a real directory and a symlink pointing to it.
            string realDir = Path.Combine(Path.GetTempPath(), $"msbuild_test_real_{Guid.NewGuid():N}");
            string symlinkDir = Path.Combine(Path.GetTempPath(), $"msbuild_test_link_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(realDir);
                Directory.CreateSymbolicLink(symlinkDir, realDir);

                HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(
                    taskHost: true,
                    taskHostParameters: TaskHostParameters.Empty,
                    nodeReuse: false);

                // Parent using the symlink path (like $(MSBuildThisFileDirectory) would on macOS /tmp)
                var symlinkHandshake = new Handshake(options, symlinkDir);

                // Child using the resolved real path (like AppContext.BaseDirectory resolves /private/tmp)
                var realHandshake = new Handshake(options, realDir);

                // These produce DIFFERENT keys — this is the bug.
                // If these were used as parent vs child toolsDirectory, the pipe names would
                // differ and the parent could never connect to the child → MSB4216.
                symlinkHandshake.GetKey().ShouldNotBe(realHandshake.GetKey(),
                    "Symlinked and resolved paths should produce different handshake keys " +
                    "(they are different strings). This demonstrates why the parent must NOT " +
                    "use an MSBuild property path that may contain unresolved symlinks — it " +
                    "must use MSBuildToolsDirectoryRoot (same source as the child) instead.");

                // Using the SAME source (MSBuildToolsDirectoryRoot) on both sides always matches,
                // regardless of symlinks, because both compute it from AppContext.BaseDirectory.
                string consistentDir = BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot;
                var parentFixed = new Handshake(options, consistentDir);
                var childDefault = new Handshake(options);

                parentFixed.GetKey().ShouldBe(childDefault.GetKey(),
                    "Using MSBuildToolsDirectoryRoot on both sides must produce matching keys.");
            }
            finally
            {
                if (Directory.Exists(symlinkDir))
                {
                    Directory.Delete(symlinkDir);
                }

                if (Directory.Exists(realDir))
                {
                    Directory.Delete(realDir);
                }
            }
        }
#endif
    }
}
