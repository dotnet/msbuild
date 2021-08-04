// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class ValidatePackageTargetTests : SdkTest
    {
        public ValidatePackageTargetTests(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void InvalidPackage()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:ForceValidationProblem=true");

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("error CP0002: Member 'PackageValidationTestProject.Program.SomeAPINotIn6_0()' exists on lib/netstandard2.0/PackageValidationTestProject.dll but not on lib/net6.0/PackageValidationTestProject.dll", result.StdOut);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ValidatePackageTargetRunsSuccessfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute();

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(0, result.ExitCode);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ValidatePackageTargetRunsSuccessfullyWithBaselineCheck()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselinePath={packageValidationBaselinePath}");

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(0, result.ExitCode);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ValidatePackageTargetRunsSuccessfullyWithBaselineVersion()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselineVersion=1.0.0;PackageValidationBaselineName=PackageValidationTestProject");

            // No failures while running the package validation on a simple assembly.
            Assert.Equal(0, result.ExitCode);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ValidatePackageTargetFailsWithBaselineVersion()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;AddBreakingChange=true;PackageValidationBaselinePath={packageValidationBaselinePath}");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("error CP0002: Member 'PackageValidationTestProject.Program.SomeApiNotInLatestVersion()' exists on [Baseline] lib/net6.0/PackageValidationTestProject.dll but not on lib/net6.0/PackageValidationTestProject.dll", result.StdOut);
            Assert.Contains("error CP0002: Member 'PackageValidationTestProject.Program.SomeApiNotInLatestVersion()' exists on [Baseline] lib/netstandard2.0/PackageValidationTestProject.dll but not on lib/netstandard2.0/PackageValidationTestProject.dll", result.StdOut);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ValidatePackageTargetWithIncorrectBaselinePackagePath()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            string nonExistentPackageBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;PackageValidationBaselinePath={nonExistentPackageBaselinePath}");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"Could not find file '{nonExistentPackageBaselinePath}'. ", result.StdOut);

            // Disables package baseline validation.
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;DisablePackageBaselineValidation=true;PackageValidationBaselinePath={nonExistentPackageBaselinePath}");
            Assert.Equal(0, result.ExitCode);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ValidatePackageWithReferences()
        {
            TestLogger log = new TestLogger();

            string subDependencyName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testSubDependency = new()
            {
                Name = subDependencyName,
                TargetFrameworks = "netstandard2.0"
            };
            string subDependencySourceCode = @"
namespace PackageValidationTests
{
    public interface IBaseInterface { }
}
";
            testSubDependency.SourceFiles.Add("IBaseInterface.cs", subDependencySourceCode);

            string dependencyName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testDependency = new()
            {
                Name = dependencyName,
                TargetFrameworks = "netstandard2.0;net5.0"
            };
            string dependencySourceCode = @"
namespace PackageValidationTests
{
    public class ItermediateBaseClass
#if NETSTANDARD2_0
: IBaseInterface
#endif
    { }
}
";
            testDependency.ReferencedProjects.Add(testSubDependency);
            testDependency.SourceFiles.Add("ItermediateBaseClass.cs", dependencySourceCode);


            string name = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testProject = new()
            {
                Name = name,
                TargetFrameworks = "netstandard2.0;net5.0",
            };

            string sourceCode = @"
namespace PackageValidationTests
{
    public class First : ItermediateBaseClass { }
}";

            testProject.ReferencedProjects.Add(testDependency);
            testProject.SourceFiles.Add("Hello.cs", sourceCode);
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = NupkgParser.CreatePackage(packCommand.GetNuGetPackage(), null);

            // First we run without references. Without references, ApiCompat should not be able to see that class First
            // removed an interface due to it's base class removing that implementation. We validate that APICompat doesn't
            // log errors when not using references.
            new CompatibleFrameworkInPackageValidator("CP1003", null, false, log, null).Validate(package);
            Assert.Empty(log.errors);

            // Now we do pass in references. With references, ApiCompat should now detect that an interface was removed in a
            // dependent assembly, causing one of our types to stop implementing that assembly. We validate that a CP0008 is logged.
            Dictionary<string, HashSet<string>> references = new()
            {
                { "netstandard2.0", new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { "net5.0", new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "net5.0") } }
            };
            new CompatibleFrameworkInPackageValidator("CP1002", null, false, log, references).Validate(package);
            Assert.NotEmpty(log.errors);

            Assert.Contains($"CP0008 Type 'PackageValidationTests.First' does not implement interface 'PackageValidationTests.IBaseInterface' on lib/net5.0/{asset.TestProject.Name}.dll but it does on lib/netstandard2.0/{asset.TestProject.Name}.dll" ,log.errors);
        }
    }
}
