// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class CompatibleFrameworkValidatorTests : SdkTest
    {
        private TestLogger _log;

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

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
            Assert.Single(_log.errors);
            Assert.Equal("PKV004 There is no compatible runtime asset for target framework .NETCoreApp,Version=v3.1 in the package.", _log.errors[0]);
        }

        [Fact]
        public void MissingAssetForFramework()
        {
            string[] filePaths = new[]
            {
                @"ref/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package); ;
            Assert.NotEmpty(_log.errors);
            Assert.Contains("PKV004 There is no compatible runtime asset for target framework .NETStandard,Version=v2.0 in the package.", _log.errors);
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

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
            Assert.NotEmpty(_log.errors);
            Assert.Contains("PKV004 There is no compatible runtime asset for target framework .NETCoreApp,Version=v2.0 in the package.", _log.errors);
            Assert.Contains("PKV005 There is no compatible runtime asset for target framework .NETCoreApp,Version=v2.0-win in the package.", _log.errors);
        }

        [Fact]
        public void OnlyRuntimeAssembly()
        {
            string[] filePaths = new[]
            {
                @"runtimes/win/lib/netstandard2.0/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);

            Assert.NotEmpty(_log.errors);
            Assert.Contains("PKV0001 There is no compatible compile time asset for target framework .NETStandard,Version=v2.0 in the package.", _log.errors);
        }

        [Fact]
        public void LibAndRuntimeAssembly()
        {
            string[] filePaths = new[]
            {
                @"lib/netcoreapp3.1/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.1/TestPackage.dll",
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
            Assert.Empty(_log.errors);
        }

        [Fact]
        public void NoCompileTimeAssetForSpecificFramework()
        {
            string[] filePaths = new[]
            {
                @"ref/netcoreapp3.0/TestPackage.dll",
                @"lib/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.1/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
            Assert.NotEmpty(_log.errors);
            Assert.Contains("PKV0001 There is no compatible compile time asset for target framework .NETStandard,Version=v2.0 in the package.", _log.errors);
        }

        [Fact]
        public void NoRuntimeAssetForSpecificFramework()
        {
            string[] filePaths = new[]
            {
                @"ref/netcoreapp3.0/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.0/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
            Assert.NotEmpty(_log.errors);
            Assert.Contains("PKV004 There is no compatible runtime asset for target framework .NETCoreApp,Version=v3.0 in the package.", _log.errors);
        }

        [Fact]
        public void NoRuntimeSpecificAssetForSpecificFramework()
        {
            string[] filePaths = new[]
            {
                @"lib/netstandard2.0/TestPackage.dll",
                @"lib/netcoreapp3.0/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.0/TestPackage.dll",
                @"runtimes/unix/lib/netcoreapp3.0/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
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

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
            Assert.NotEmpty(_log.errors);
            Assert.Contains("PKV0001 There is no compatible compile time asset for target framework .NETStandard,Version=v2.0 in the package.", _log.errors);
        }

        [Fact]
        public void CompatibleRidSpecificAsset()
        {
            string[] filePaths = new[]
            {
                @"lib/netcoreapp2.0/TestPackage.dll",
                @"lib/netcoreapp3.0/TestPackage.dll",
                @"runtimes/win/lib/netcoreapp3.0/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
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
                @"lib/net5.0/TestPackage.dll"
            };

            Package package = new("TestPackage", "1.0.0", filePaths, null, null);
            new CompatibleTfmValidator(string.Empty, null, false, _log).Validate(package);
            Assert.Empty(_log.errors);
        }

    }
}
