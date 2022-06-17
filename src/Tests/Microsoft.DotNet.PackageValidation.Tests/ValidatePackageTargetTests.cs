// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.PackageValidation.Validators;
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

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
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

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
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

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
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

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
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

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
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

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
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

            string testDependencySource = @"namespace PackageValidationTests { public class ItermediateBaseClass
#if NETSTANDARD2_0
: IBaseInterface 
#endif
{ } }";

            TestProject testSubDependency = CreateTestProject(@"namespace PackageValidationTests { public interface IBaseInterface { } }", "netstandard2.0");
            TestProject testDependency = CreateTestProject(
                                            testDependencySource,
                                            $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}",
                                            new[] { testSubDependency });
            TestProject testProject = CreateTestProject(@"namespace PackageValidationTests { public class First : ItermediateBaseClass { } }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { testDependency });

            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage(), null);

            // First we run without references. Without references, ApiCompat should not be able to see that class First
            // removed an interface due to it's base class removing that implementation. We validate that APICompat doesn't
            // log errors when not using references.
            new CompatibleFrameworkInPackageValidator(log).Validate(new PackageValidatorOption(package));
            Assert.Empty(log.errors);

            // Now we do pass in references. With references, ApiCompat should now detect that an interface was removed in a
            // dependent assembly, causing one of our types to stop implementing that assembly. We validate that a CP0008 is logged.
            Dictionary<string, HashSet<string>> references = new()
            {
                { "netstandard2.0", new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { ToolsetInfo.CurrentTargetFramework, new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            new CompatibleFrameworkInPackageValidator(log).Validate(new PackageValidatorOption(package, frameworkReferences: references));
            Assert.NotEmpty(log.errors);

            Assert.Contains($"CP0008 Type 'PackageValidationTests.First' does not implement interface 'PackageValidationTests.IBaseInterface' on lib/{ToolsetInfo.CurrentTargetFramework}/{asset.TestProject.Name}.dll but it does on lib/netstandard2.0/{asset.TestProject.Name}.dll" ,log.errors);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void ValidateOnlyErrorWhenAReferenceIsRequired(bool createDependencyToDummy, bool useReferences, bool shouldLogError)
        {
            TestLogger log = new TestLogger();

            string testDependencyCode = createDependencyToDummy ?
                                        @"namespace PackageValidationTests{public class SomeBaseClass : IDummyInterface { }public class SomeDummyClass : IDummyInterface { }}" :
                                        @"namespace PackageValidationTests{public class SomeBaseClass { }public class SomeDummyClass : IDummyInterface { }}";

            TestProject testDummyDependency = CreateTestProject(@"namespace PackageValidationTests { public interface IDummyInterface { } }", "netstandard2.0");
            TestProject testDependency = CreateTestProject( testDependencyCode, "netstandard2.0", new[] { testDummyDependency });
            TestProject testProject = CreateTestProject(@"namespace PackageValidationTests { public class First : SomeBaseClass { } }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { testDependency });

            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage(), null);

            Dictionary<string, HashSet<string>> references = new()
            {
                { "netstandard2.0", new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { ToolsetInfo.CurrentTargetFramework, new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };

            File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{testDummyDependency.Name}.dll"));

            // First we run without references. Without references, ApiCompat should not be able to see that class First
            // removed an interface due to it's base class removing that implementation. We validate that APICompat doesn't
            // log errors when not using references.
            new CompatibleFrameworkInPackageValidator(log).Validate(new PackageValidatorOption(package, frameworkReferences: useReferences ? references : null));
            if (shouldLogError)
                Assert.Contains($"CP1002 Could not find matching assembly: '{testDummyDependency.Name}.dll' in any of the search directories.", log.errors);
            else
                Assert.DoesNotContain($"CP1002 Could not find matching assembly: '{testDummyDependency.Name}.dll' in any of the search directories." ,log.errors);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(false, true, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(true, true, true, true)]
        public void ValidateErrorWhenTypeForwardingReferences(bool useReferences, bool expectCP0001, bool deleteFile, bool expectCP1002)
        {
            TestLogger log = new TestLogger();

            string dependencySourceCode = @"namespace PackageValidationTests { public interface ISomeInterface { }
#if !NETSTANDARD2_0
public class MyForwardedType : ISomeInterface { }
#endif
}";
            string testSourceCode = @"
#if NETSTANDARD2_0
namespace PackageValidationTests { public class MyForwardedType : ISomeInterface { } }
#else
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(PackageValidationTests.MyForwardedType))]
#endif";
            TestProject dependency = CreateTestProject(dependencySourceCode, $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}");
            TestProject testProject = CreateTestProject(testSourceCode, $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { dependency });

            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage(), null);

            Dictionary<string, HashSet<string>> references = new()
            {
                { "netstandard2.0", new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { ToolsetInfo.CurrentTargetFramework, new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };

            if (deleteFile)
                File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{dependency.Name}.dll"));

            new CompatibleFrameworkInPackageValidator(log).Validate(new PackageValidatorOption(package, frameworkReferences: useReferences ? references : null));

            if (expectCP0001)
                Assert.Contains($"CP0001 Type 'PackageValidationTests.MyForwardedType' exists on lib/netstandard2.0/{testProject.Name}.dll but not on lib/{ToolsetInfo.CurrentTargetFramework}/{testProject.Name}.dll", log.errors);

            if (expectCP1002)
                Assert.Contains($"CP1002 Could not find matching assembly: '{dependency.Name}.dll' in any of the search directories.", log.errors);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void EnsureOnlyOneAssemblyLoadErrorIsLoggedPerMissingAssembly()
        {
            TestLogger log = new TestLogger();

            string dependencySourceCode = @"namespace PackageValidationTests { public interface ISomeInterface { }
#if !NETSTANDARD2_0
public class MyForwardedType : ISomeInterface { }
public class MySecondForwardedType : ISomeInterface { }
#endif
}";
            string testSourceCode = @"
#if NETSTANDARD2_0
namespace PackageValidationTests { public class MyForwardedType : ISomeInterface { } public class MySecondForwardedType : ISomeInterface { } }
#else
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(PackageValidationTests.MyForwardedType))]
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(PackageValidationTests.MySecondForwardedType))]
#endif";

            TestProject dependency = CreateTestProject(dependencySourceCode, $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}");
            TestProject testProject = CreateTestProject(testSourceCode, $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { dependency });

            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage(), null);

            Dictionary<string, HashSet<string>> references = new()
            {
                { "netstandard2.0", new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { ToolsetInfo.CurrentTargetFramework, new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };

            File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{dependency.Name}.dll"));

            new CompatibleFrameworkInPackageValidator(log).Validate(new PackageValidatorOption(package, frameworkReferences: references));

            Assert.Single(log.errors.Where(e => e.Contains("CP1002")));
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(true)]
        [InlineData(false)]
        public void ValidateMissingReferencesIsOnlyLoggedWhenRunningWithReferences(bool useReferences)
        {
            TestLogger log = new TestLogger();

            TestProject testProject = CreateTestProject("public class MyType { }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}");
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new PackCommand(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage(), null);

            Dictionary<string, HashSet<string>> references = new()
            {
                { "netstandard2.0", new HashSet<string> { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } }
            };

            new CompatibleFrameworkInPackageValidator(log).Validate(new PackageValidatorOption(package, frameworkReferences: useReferences ? references : null));

            if (!useReferences)
                Assert.Empty(log.errors.Where(e => e.Contains("CP1003")));
            else
                Assert.NotEmpty(log.errors.Where(e => e.Contains("CP1003")));
        }

        private TestProject CreateTestProject(string sourceCode, string tfms, IEnumerable<TestProject> referenceProjects = null)
        {
            string name = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testProject = new()
            {
                Name = name,
                TargetFrameworks = tfms,
            };

            testProject.SourceFiles.Add($"{name}.cs", sourceCode);

            if (referenceProjects != null)
            {
                foreach (var project in referenceProjects)
                    testProject.ReferencedProjects.Add(project);
            }

            return testProject;
        }
    }
}
