// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Validators;

namespace Microsoft.DotNet.ApiCompat.IntegrationTests
{
    public class CompatibleFrameworkInPackageValidatorIntegrationTests : SdkTest
    {
        public CompatibleFrameworkInPackageValidatorIntegrationTests(ITestOutputHelper log) : base(log)
        {
        }

        private (SuppressableTestLog, CompatibleFrameworkInPackageValidator) CreateLoggerAndValidator()
        {
            SuppressableTestLog log = new();
            CompatibleFrameworkInPackageValidator validator = new(log,
                new ApiCompatRunner(log,
                    new SuppressionEngine(),
                    new ApiComparerFactory(new RuleFactory(log)),
                    new AssemblySymbolLoaderFactory()));

            return (log, validator);
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
            (SuppressableTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            Assert.NotEmpty(log.errors);
            // TODO: add asserts for assembly and header metadata.
            string assemblyName = $"{asset.TestProject.Name}.dll";
            Assert.Contains($"CP0002 Member 'void PackageValidationTests.First.test(string)' exists on lib/netstandard2.0/{assemblyName} but not on lib/{ToolsetInfo.CurrentTargetFramework}/{assemblyName}", log.errors);
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
            (SuppressableTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            Assert.NotEmpty(log.errors);
            string assemblyName = $"{asset.TestProject.Name}.dll";
            // TODO: add asserts for assembly and header metadata.
            Assert.Contains($"CP0002 Member 'void PackageValidationTests.First.test(string)' exists on lib/netstandard2.0/{assemblyName} but not on lib/netcoreapp3.1/{assemblyName}", log.errors);
            Assert.Contains($"CP0002 Member 'void PackageValidationTests.First.test(bool)' exists on lib/netcoreapp3.1/{assemblyName} but not on lib/{ToolsetInfo.CurrentTargetFramework}/{assemblyName}", log.errors);
        }
    }
}
