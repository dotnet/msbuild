// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

using Shouldly;

using Xunit;

namespace Microsoft.Build.Engine.UnitTests
{
    /// <summary>
    /// Tests for the <c>VerifyBootstrapRuntimeFloor</c> target in <c>eng/BootStrapMsBuild.targets</c>.
    /// The target guards against laying down a bootstrap whose .NET runtime is older than the runtime
    /// delivered by the SDK building this repository: build outputs embed that runtime version, so an
    /// older bootstrap runtime causes host-resolution failures.
    /// </summary>
    public sealed class BootstrapRuntimeFloor_Tests
    {
        private readonly ITestOutputHelper _output;

        public BootstrapRuntimeFloor_Tests(ITestOutputHelper output) => _output = output;

        [Theory]
        [InlineData("10.0.8", "10.0.8")]   // Exactly the floor is acceptable.
        [InlineData("10.0.8", "10.0.9")]   // A newer patch satisfies the floor.
        [InlineData("10.0.8", "10.0.10")]  // Comparison is numeric, not lexical (10 > 8).
        [InlineData("9.0.100", "10.0.0")]  // A newer major satisfies the floor.
        [InlineData("10.0.0-preview.7.25301.110", "10.0.0-preview.4.25258.110")] // Prerelease suffix is ignored; numeric components are equal.
        public void Passes_WhenBootstrapRuntimeAtLeastFloor(string bundledRuntime, string detectedRuntime)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = CreateBootstrapWithRuntimes(env, detectedRuntime);

            bool success = RunGuard(artifactsBinDir, bundledRuntime, out MockLogger logger);

            success.ShouldBeTrue();
            logger.Errors.ShouldBeEmpty();
        }

        [Fact]
        public void Fails_WhenBootstrapRuntimeOlderThanFloor()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = CreateBootstrapWithRuntimes(env, "9.7.2");

            bool success = RunGuard(artifactsBinDir, "10.0.8", out MockLogger logger);

            success.ShouldBeFalse();
            BuildErrorEventArgs error = logger.Errors.ShouldHaveSingleItem();

            // The message is hardcoded (not localized), so asserting on the embedded versions is locale-safe.
            // "9.7.2" is deliberately not a substring of the SDK version (10.0.300) or the floor (10.0.8),
            // so this proves the detected-runtime list actually renders into the message.
            string message = error.Message ?? string.Empty;
            message.ShouldContain("9.7.2");
            message.ShouldContain("10.0.8");
        }

        [Fact]
        public void Fails_WhenAllPresentRuntimesAreOlderThanFloor()
        {
            // Multiple older runtimes: a single error should list them all (validates the plural,
            // ';'-joined @(_BootstrapRuntimeVersion) rendering in the message).
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = CreateBootstrapWithRuntimes(env, "9.0.5", "9.7.2");

            bool success = RunGuard(artifactsBinDir, "10.0.8", out MockLogger logger);

            success.ShouldBeFalse();
            BuildErrorEventArgs error = logger.Errors.ShouldHaveSingleItem();
            string message = error.Message ?? string.Empty;
            message.ShouldContain("9.0.5");
            message.ShouldContain("9.7.2");
        }

        [Fact]
        public void Passes_WhenAnyPresentRuntimeSatisfiesFloor()
        {
            // The bootstrap host rolls forward to the highest compatible runtime, so the guard is
            // satisfied as long as at least one laid-down runtime meets the floor.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = CreateBootstrapWithRuntimes(env, "10.0.3", "10.0.9");

            bool success = RunGuard(artifactsBinDir, "10.0.8", out MockLogger logger);

            success.ShouldBeTrue();
            logger.Errors.ShouldBeEmpty();
        }

        [Fact]
        public void Passes_WhenNoBootstrapRuntimeIsPresent()
        {
            // Defensive: the guard fails only when it positively detects an older runtime. If the
            // shared framework folder is absent (for example, the layout changed), it must not
            // false-positive and break the build.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = env.CreateFolder().Path;

            bool success = RunGuard(artifactsBinDir, "10.0.8", out MockLogger logger);

            success.ShouldBeTrue();
            logger.Errors.ShouldBeEmpty();
        }

        [Fact]
        public void Passes_WhenFloorIsUnknown()
        {
            // On SDKs/configs where BundledNETCoreAppPackageVersion is unset, the guard has no floor
            // to compare against and must no-op - even when an older runtime is present.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = CreateBootstrapWithRuntimes(env, "9.0.0");

            bool success = RunGuard(artifactsBinDir, bundledRuntime: string.Empty, out MockLogger logger);

            success.ShouldBeTrue();
            logger.Errors.ShouldBeEmpty();
        }

        [Fact]
        public void Passes_WhenSharedRuntimeRootIsEmpty()
        {
            // The shared-framework folder exists but no runtime was laid down yet: distinct from the
            // absent-folder path, and must also no-op rather than false-positive.
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = CreateBootstrapWithRuntimes(env);

            bool success = RunGuard(artifactsBinDir, "10.0.8", out MockLogger logger);

            success.ShouldBeTrue();
            logger.Errors.ShouldBeEmpty();
        }

        [Fact]
        public void Passes_WhenOnlyNonVersionDirectoriesPresent()
        {
            // A stray non-version directory must be ignored, not fed into the version comparison
            // (which would otherwise throw and fail the build cryptically).
            using TestEnvironment env = TestEnvironment.Create(_output);
            string artifactsBinDir = CreateBootstrapWithRuntimes(env, "host");

            bool success = RunGuard(artifactsBinDir, "10.0.8", out MockLogger logger);

            success.ShouldBeTrue();
            logger.Errors.ShouldBeEmpty();
        }

        private static string CreateBootstrapWithRuntimes(TestEnvironment env, params string[] runtimeVersions)
        {
            string artifactsBinDir = env.CreateFolder().Path;
            string sharedRoot = Path.Combine(artifactsBinDir, "bootstrap", "core", "shared", "Microsoft.NETCore.App");

            // Always create the shared-framework root so callers can exercise the "root exists but is
            // empty" path by passing no versions.
            Directory.CreateDirectory(sharedRoot);
            foreach (string version in runtimeVersions)
            {
                Directory.CreateDirectory(Path.Combine(sharedRoot, version));
            }

            return artifactsBinDir;
        }

        private bool RunGuard(string artifactsBinDir, string bundledRuntime, out MockLogger logger)
        {
            string targetsFile = LocateRepositoryEngFile("BootStrapMsBuild.targets");

            string projectXml = $"""
                <Project>
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <BootstrapSdkVersion>10.0.300</BootstrapSdkVersion>
                    <ArtifactsBinDir>{SecurityElement.Escape(artifactsBinDir)}{Path.DirectorySeparatorChar}</ArtifactsBinDir>
                    <BundledNETCoreAppPackageVersion>{SecurityElement.Escape(bundledRuntime)}</BundledNETCoreAppPackageVersion>
                  </PropertyGroup>
                  <Import Project="{SecurityElement.Escape(targetsFile)}" />
                </Project>
                """;

            using ProjectCollection collection = new();
            using ProjectFromString projectFromString = new(projectXml, null, null, collection);
            logger = new MockLogger(_output);

            // Build the guard target by name. Because it is hooked via AfterTargets="AcquireSdk"
            // (rather than depended upon), invoking it directly does not trigger an SDK download.
            return projectFromString.Project.Build("VerifyBootstrapRuntimeFloor", [logger]);
        }

        private static string LocateRepositoryEngFile(string fileName)
        {
            for (string? dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, "eng", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException($"Could not locate eng/{fileName} by walking up from '{AppContext.BaseDirectory}'.");
        }
    }
}
