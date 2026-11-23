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

                // This might seem redundant, but it's not.
                // ClearBootstrapDotnetRootEnvironment is a production method that alters env variables directly via Environment class.
                // This clears DOTNET_ROOT_X86 which might have been originally set.
                // To ensure that DOTNET_ROOT_X86 is restored, we set it to itself so that TestEnvironment.Dispose restores it back.
                env.SetEnvironmentVariable("DOTNET_ROOT_X86", Environment.GetEnvironmentVariable("DOTNET_ROOT_X86"));

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
        /// Regression test for the macOS /tmp → /private/tmp symlink issue (MSB4216).
        ///
        /// Before the fix, the parent passed $(NetCoreSdkRoot) as toolsDirectory —
        /// an MSBuild property that can contain unresolved symlinks. The child always
        /// defaults to BuildEnvironmentHelper (which resolves symlinks via
        /// AppContext.BaseDirectory). This caused different handshake hashes.
        ///
        /// After the fix (on .NET Core), the parent also omits toolsDirectory,
        /// so both sides default to BuildEnvironmentHelper.
        ///
        /// This test proves that an arbitrary external path (simulating $(NetCoreSdkRoot))
        /// CAN produce a different handshake than the BuildEnvironmentHelper default,
        /// and that omitting toolsDirectory on both sides always matches.
        /// </summary>
#if NET
        [Fact]
        public void Handshake_ExternalPathCanMismatch_DefaultAlwaysMatches()
        {
            // Use explicit NET runtime and current architecture to ensure the NET
            // HandshakeOptions flag is set, which is required for passing toolsDirectory
            // to the Handshake constructor.
            var netTaskHostParams = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.net,
                architecture: XMakeAttributes.GetCurrentMSBuildArchitecture(),
                dotnetHostPath: null,
                msBuildAssemblyPath: null);

            HandshakeOptions options = CommunicationsUtilities.GetHandshakeOptions(
                taskHost: true,
                taskHostParameters: netTaskHostParams,
                nodeReuse: false);

            // Simulate child: no explicit toolsDirectory → defaults to BuildEnvironmentHelper.
            var childHandshake = new Handshake(options);

            // After the fix: parent also omits toolsDirectory → same default → must match.
            var parentFixedHandshake = new Handshake(options);
            parentFixedHandshake.GetKey().ShouldBe(childHandshake.GetKey(),
                "When both parent and child omit toolsDirectory, they must produce " +
                "identical handshake keys (both default to BuildEnvironmentHelper).");

            // Before the fix: parent passed an external path ($(NetCoreSdkRoot)).
            // If that path differs from BuildEnvironmentHelper (e.g. symlinks),
            // the handshake would mismatch.
            string externalPath = Path.Combine(Path.GetTempPath(), $"different_path_{Guid.NewGuid():N}");
            var parentBrokenHandshake = new Handshake(options, externalPath);
            parentBrokenHandshake.GetKey().ShouldNotBe(childHandshake.GetKey(),
                "An arbitrary external toolsDirectory should produce a different handshake " +
                "than the BuildEnvironmentHelper default, proving the mismatch scenario.");
        }

        /// <summary>
        /// Regression test for MSB4216 caused by drive-letter casing differences on Windows. The
        /// .NET Framework parent passes the SDK directory ($(NetCoreSdkRoot)) as the tools directory
        /// while the child re-resolves the same directory from its own process path. On hosted CI
        /// agents these can differ only by drive-letter casing ("D:\..." vs "d:\..."). Because the
        /// salt is a case-sensitive hash they would mismatch, so the child accepts the parent's
        /// drive-letter casing. This only widens what the child accepts, so it never rejects a parent
        /// that connects successfully today.
        /// </summary>
        [WindowsOnlyFact]
        public void Handshake_NetTaskHost_AcceptsDriveLetterCasingDifference()
        {
            HandshakeOptions options = GetNetTaskHostHandshakeOptions();

            // The child resolved its own location with one drive-letter casing; the parent supplied
            // the same directory with the opposite casing.
            var child = new Handshake(options, @"d:\agent\_work\sdk\11.0.100");
            var parent = new Handshake(options, @"D:\agent\_work\sdk\11.0.100");

            int childSalt = child.RetrieveHandshakeComponents().Salt;
            int parentSalt = parent.RetrieveHandshakeComponents().Salt;

            // The raw salts differ because the hash is case-sensitive...
            parentSalt.ShouldNotBe(childSalt,
                "Drive-letter casing differences must produce different case-sensitive salts; " +
                "otherwise this test would not exercise the tolerance.");

            // ...but the child tolerates the parent's drive-letter casing.
            child.IsNetTaskHostSaltMatch(parentSalt).ShouldBeTrue(
                "The .NET task host child must accept a parent that resolved the same SDK directory " +
                "with a different drive-letter casing, otherwise the handshake fails with MSB4216.");
        }

        /// <summary>
        /// The drive-letter tolerance must not weaken node-reuse isolation: a genuinely different
        /// tools directory (not merely a drive-letter casing variant) must still be rejected.
        /// </summary>
        [WindowsOnlyFact]
        public void Handshake_NetTaskHost_RejectsUnrelatedToolsDirectory()
        {
            HandshakeOptions options = GetNetTaskHostHandshakeOptions();

            var child = new Handshake(options, @"d:\agent\_work\sdk\11.0.100");
            var unrelated = new Handshake(options, @"d:\agent\_work\sdk\10.0.200");

            child.IsNetTaskHostSaltMatch(unrelated.RetrieveHandshakeComponents().Salt).ShouldBeFalse(
                "A different SDK directory is not a drive-letter casing variant and must not be accepted.");
        }

        /// <summary>
        /// The drive-letter tolerance is scoped to the .NET task host. Other node types keep
        /// requiring the exact salt, preserving existing node-reuse isolation.
        /// </summary>
        [WindowsOnlyFact]
        public void Handshake_NonNetTaskHost_DoesNotWidenSaltAcceptance()
        {
            var handshake = new Handshake(HandshakeOptions.NodeReuse);

            // Even the node's own salt must not be treated as a drive-letter variant match, because
            // a non-.NET-task-host node never computes an alternate-casing salt.
            handshake.IsNetTaskHostSaltMatch(handshake.RetrieveHandshakeComponents().Salt).ShouldBeFalse(
                "Only the .NET task host computes an alternate-casing salt; other node types must " +
                "not widen salt acceptance.");
        }

        private static HandshakeOptions GetNetTaskHostHandshakeOptions()
        {
            // Use explicit NET runtime and current architecture to ensure the NET HandshakeOptions
            // flag is set, which is required for passing toolsDirectory to the Handshake constructor.
            var netTaskHostParams = new TaskHostParameters(
                runtime: XMakeAttributes.MSBuildRuntimeValues.net,
                architecture: XMakeAttributes.GetCurrentMSBuildArchitecture(),
                dotnetHostPath: null,
                msBuildAssemblyPath: null);

            return CommunicationsUtilities.GetHandshakeOptions(
                taskHost: true,
                taskHostParameters: netTaskHostParams,
                nodeReuse: false);
        }
#endif

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

        [Fact]
        public void ResolveNetTaskHostLaunchPath_ReturnsAppHost_WhenAppHostFileExists()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string sdkPath = env.CreateFolder().Path;
            string appHostPath = Path.Combine(sdkPath, Constants.MSBuildExecutableName);
            File.WriteAllText(appHostPath, string.Empty);
            File.WriteAllText(Path.Combine(sdkPath, Constants.MSBuildAssemblyName), string.Empty);

            (string launchPath, bool useAppHost) = NodeProviderOutOfProcTaskHost.ResolveNetTaskHostLaunchPath(sdkPath);

            useAppHost.ShouldBeTrue();
            launchPath.ShouldBe(appHostPath);
        }

        [Fact]
        public void ResolveNetTaskHostLaunchPath_FallsBackToMSBuildDll_WhenAppHostMissing()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string sdkPath = env.CreateFolder().Path;
            string msbuildDllPath = Path.Combine(sdkPath, Constants.MSBuildAssemblyName);
            File.WriteAllText(msbuildDllPath, string.Empty);

            (string launchPath, bool useAppHost) = NodeProviderOutOfProcTaskHost.ResolveNetTaskHostLaunchPath(sdkPath);

            useAppHost.ShouldBeFalse();
            launchPath.ShouldBe(msbuildDllPath);
        }
    }
}
