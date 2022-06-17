// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.PackageValidation.Validators;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class CompatibleFrameworksInPackageTests : SdkTest
    {
        private TestLogger _log;

        public CompatibleFrameworksInPackageTests(ITestOutputHelper log) : base(log)
        {
            _log = new TestLogger();
        }

        [Fact]
        public void CompatibleFrameworksInPackage()
        {
            string name = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testProject = new()
            {
                Name = name,
                TargetFrameworks = $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}",
            };

            string sourceCode = @"
namespace PackageValidationTests
{
    public class First
    {
        public void test() { }
#if NETSTANDARD2_0
        public void test(string test) { }
#endif
    }
}";
            testProject.SourceFiles.Add("Hello.cs", sourceCode);
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage(), null);
            new CompatibleFrameworkInPackageValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(_log.errors);
            // TODO: add asserts for assembly and header metadata.
            string assemblyName = $"{asset.TestProject.Name}.dll";
            Assert.Contains($"CP0002 Member 'PackageValidationTests.First.test(string)' exists on lib/netstandard2.0/{assemblyName} but not on lib/{ToolsetInfo.CurrentTargetFramework}/{assemblyName}", _log.errors);
        }
        
        [Fact]
        public void MultipleCompatibleFrameworksInPackage()
        {
            string name = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testProject = new()
            {
                Name = name,
                TargetFrameworks = $"netstandard2.0;netcoreapp3.1;{ToolsetInfo.CurrentTargetFramework}",
            };

            string sourceCode = @"
namespace PackageValidationTests
{
    public class First
    {
        public void test() { }
#if NETSTANDARD2_0
        public void test(string test) { }
#endif
#if NETCOREAPP3_1
        public void test(bool test) { }
#endif
    }
}";

            testProject.SourceFiles.Add("Hello.cs", sourceCode);
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage(), null);
            new CompatibleFrameworkInPackageValidator(_log).Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(_log.errors);

            string assemblyName = $"{asset.TestProject.Name}.dll";
            // TODO: add asserts for assembly and header metadata.
            Assert.Contains($"CP0002 Member 'PackageValidationTests.First.test(string)' exists on lib/netstandard2.0/{assemblyName} but not on lib/netcoreapp3.1/{assemblyName}", _log.errors);
            Assert.Contains($"CP0002 Member 'PackageValidationTests.First.test(bool)' exists on lib/netcoreapp3.1/{assemblyName} but not on lib/{ToolsetInfo.CurrentTargetFramework}/{assemblyName}", _log.errors);
        }
    }
}
