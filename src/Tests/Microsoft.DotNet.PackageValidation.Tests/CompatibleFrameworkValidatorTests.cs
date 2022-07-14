// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.PackageValidation.Validators;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class CompatibleFrameworkValidatorTests : SdkTest
    {
        private readonly TestLogger _log;

        public CompatibleFrameworkValidatorTests(ITestOutputHelper log) : base(log)
        {
            _log = new TestLogger();
        }

        [Fact]
        public void MissingRidLessAssetForFramework()
        {
            string[] filePaths = new[]
            {
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.Single(_log.errors);
            Assert.Equal(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + string.Format(Resources.NoCompatibleRuntimeAsset, ".NETCoreApp,Version=v3.1"), _log.errors[0]);
        }

        [Fact]
        public void MissingAssetForFramework()
        {
            string[] filePaths = new[]
            {
                @"ref/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(_log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + string.Format(Resources.NoCompatibleRuntimeAsset, ".NETStandard,Version=v2.0"), _log.errors);
        }

        [Fact]
        public void MissingRidSpecificAssetForFramework()
        {
            string[] filePaths = new[]
            {
                @"ref/netcoreapp2.0/TestPackage.dll",
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(_log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + string.Format(Resources.NoCompatibleRuntimeAsset, ".NETCoreApp,Version=v2.0"), _log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidSpecificAsset + " " + string.Format(Resources.NoCompatibleRidSpecificRuntimeAsset, ".NETCoreApp,Version=v2.0", "win"), _log.errors);
        }

        [Fact]
        public void OnlyRuntimeAssembly()
        {
            string[] filePaths = new[]
            {
                @"runtimes/win/lib/netstandard2.0/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));

            Assert.NotEmpty(_log.errors);
            Assert.Contains(DiagnosticIds.ApplicableCompileTimeAsset + " " + string.Format(Resources.NoCompatibleCompileTimeAsset, ".NETStandard,Version=v2.0"), _log.errors);
        }

        [Fact]
        public void LibAndRuntimeAssembly()
        {
            string[] filePaths = new[]
            {
                @"lib/netcoreapp3.1/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll",
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.Empty(_log.errors);
        }

        [Fact]
        public void NoCompileTimeAssetForSpecificFramework()
        {
            string[] filePaths = new[]
            {
                $@"ref/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(_log.errors);
            Assert.Contains(DiagnosticIds.ApplicableCompileTimeAsset + " " +string.Format(Resources.NoCompatibleCompileTimeAsset, ".NETStandard,Version=v2.0"), _log.errors);
        }

        [Fact]
        public void NoRuntimeAssetForSpecificFramework()
        {
            string[] filePaths = new[]
            {
                $@"ref/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/win/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(_log.errors);
            Assert.Contains(DiagnosticIds.CompatibleRuntimeRidLessAsset +  " " + string.Format(Resources.NoCompatibleRuntimeAsset, ToolsetInfo.CurrentTargetFramework), _log.errors);
        }

        [Fact]
        public void NoRuntimeSpecificAssetForSpecificFramework()
        {
            string[] filePaths = new[]
            {
                @"lib/netstandard2.0/TestPackage.dll",
                $@"lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/win/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/unix/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.Empty(_log.errors);
        }

        [Fact]
        public void CompatibleLibAsset()
        {
            string[] filePaths = new[]
            {
                @"ref/netcoreapp2.0/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(_log.errors);
            Assert.Contains(DiagnosticIds.ApplicableCompileTimeAsset + " " + string.Format(Resources.NoCompatibleCompileTimeAsset, ".NETStandard,Version=v2.0"), _log.errors);
        }

        [Fact]
        public void CompatibleRidSpecificAsset()
        {
            string[] filePaths = new[]
            {
                @"lib/netcoreapp2.0/TestPackage.dll",
                $@"lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll",
                $@"runtimes/win/lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.Empty(_log.errors);
        }

        [Fact]
        public void CompatibleFrameworksWithDifferentAssets()
        {
            string[] filePaths = new[]
            {
                @"ref/netstandard2.0/TestPackage.dll",
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll",
                $@"lib/{ToolsetInfo.CurrentTargetFramework}/TestPackage.dll"
            };

            Package package = new(string.Empty, "TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.Empty(_log.errors);
        }

    }
}
