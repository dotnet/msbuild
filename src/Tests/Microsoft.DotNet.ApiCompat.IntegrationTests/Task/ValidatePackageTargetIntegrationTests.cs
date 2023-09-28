// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompat.IntegrationTests;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Validators;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ApiCompat.Task.IntegrationTests
{
    public class ValidatePackageTargetIntegrationTests : SdkTest
    {
        public ValidatePackageTargetIntegrationTests(ITestOutputHelper log) : base(log)
        {
        }

        private (SuppressibleTestLog, CompatibleFrameworkInPackageValidator) CreateLoggerAndValidator()
        {
            SuppressibleTestLog log = new();
            CompatibleFrameworkInPackageValidator validator = new(log,
                new ApiCompatRunner(log,
                    new SuppressionEngine(),
                    new ApiComparerFactory(new RuleFactory(log)),
                    new AssemblySymbolLoaderFactory()));

            return (log, validator);
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
            Assert.Contains(string.Format(Resources.NonExistentPackagePath, nonExistentPackageBaselinePath), result.StdOut);

            // Disables package baseline validation.
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;DisablePackageBaselineValidation=true;PackageValidationBaselinePath={nonExistentPackageBaselinePath}");
            Assert.Equal(0, result.ExitCode);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ValidatePackageWithReferences()
        {
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
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage());
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            // First we run without references. Without references, ApiCompat should not be able to see that class First
            // removed an interface due to it's base class removing that implementation. We validate that APICompat doesn't
            // log errors when not using references.
            validator.Validate(new PackageValidatorOption(package));
            Assert.Empty(log.errors);

            // Now we do pass in references. With references, ApiCompat should now detect that an interface was removed in a
            // dependent assembly, causing one of our types to stop implementing that assembly. We validate that a CP0008 is logged.
            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseFolder(ToolsetInfo.CurrentTargetFramework), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            package = Package.Create(packCommand.GetNuGetPackage(), references);
            validator.Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(log.errors);

            Assert.Contains($"CP0008 Type 'PackageValidationTests.First' does not implement interface 'PackageValidationTests.IBaseInterface' on lib/{ToolsetInfo.CurrentTargetFramework}/{asset.TestProject.Name}.dll but it does on lib/netstandard2.0/{asset.TestProject.Name}.dll", log.errors);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void ValidateOnlyErrorWhenAReferenceIsRequired(bool createDependencyToDummy, bool useReferences, bool shouldLogError)
        {
            string testDependencyCode = createDependencyToDummy ?
                                        @"namespace PackageValidationTests{public class SomeBaseClass : IDummyInterface { }public class SomeDummyClass : IDummyInterface { }}" :
                                        @"namespace PackageValidationTests{public class SomeBaseClass { }public class SomeDummyClass : IDummyInterface { }}";

            TestProject testDummyDependency = CreateTestProject(@"namespace PackageValidationTests { public interface IDummyInterface { } }", "netstandard2.0");
            TestProject testDependency = CreateTestProject(testDependencyCode, "netstandard2.0", new[] { testDummyDependency });
            TestProject testProject = CreateTestProject(@"namespace PackageValidationTests { public class First : SomeBaseClass { } }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { testDependency });

            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseFolder(ToolsetInfo.CurrentTargetFramework), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), packageAssemblyReferences: useReferences ? references : null);

            File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{testDummyDependency.Name}.dll"));
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            // First we run without references. Without references, ApiCompat should not be able to see that class First
            // removed an interface due to it's base class removing that implementation. We validate that APICompat doesn't
            // log errors when not using references.
            validator.Validate(new PackageValidatorOption(package));
            if (shouldLogError)
                Assert.Contains($"CP1002 Could not find matching assembly: '{testDummyDependency.Name}.dll' in any of the search directories.", log.errors);
            else
                Assert.DoesNotContain($"CP1002 Could not find matching assembly: '{testDummyDependency.Name}.dll' in any of the search directories.", log.errors);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(false, true, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(true, true, true, true)]
        public void ValidateErrorWhenTypeForwardingReferences(bool useReferences, bool expectCP0001, bool deleteFile, bool expectCP1002)
        {
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
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseFolder(ToolsetInfo.CurrentTargetFramework), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), packageAssemblyReferences: useReferences ? references : null);

            if (deleteFile)
                File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{dependency.Name}.dll"));

            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            if (expectCP0001)
                Assert.Contains($"CP0001 Type 'PackageValidationTests.MyForwardedType' exists on lib/netstandard2.0/{testProject.Name}.dll but not on lib/{ToolsetInfo.CurrentTargetFramework}/{testProject.Name}.dll", log.errors);

            if (expectCP1002)
                Assert.Contains($"CP1002 Could not find matching assembly: '{dependency.Name}.dll' in any of the search directories.", log.errors);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void EnsureOnlyOneAssemblyLoadErrorIsLoggedPerMissingAssembly()
        {
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
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseFolder(ToolsetInfo.CurrentTargetFramework), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), references);

            File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{dependency.Name}.dll"));
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            Assert.Single(log.errors.Where(e => e.Contains("CP1002")));
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(true)]
        [InlineData(false)]
        public void ValidateMissingReferencesIsOnlyLoggedWhenRunningWithReferences(bool useReferences)
        {
            TestProject testProject = CreateTestProject("public class MyType { }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}");
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), useReferences ? references : null);
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            if (!useReferences)
                Assert.Empty(log.warnings.Where(e => e.Contains("CP1003")));
            else
                Assert.NotEmpty(log.warnings.Where(e => e.Contains("CP1003")));
        }

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
        public void ValidateReferencesAreRespectedForPlatformSpecificTFMs()
        {
            TestProject testProject = CreateTestProject("public class MyType { }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}-windows");
            TestAsset asset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Empty(result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.Parse("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseComponents($".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}", "Windows,Version=7.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", $"net{ToolsetInfo.CurrentTargetFrameworkVersion}-windows") } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), references);
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            Assert.Empty(log.warnings.Where(e => e.Contains("CP1003")));
        }

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
        public void ValidatePackageTargetFailsWithBaselineVersionInStrictMode()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;ForceStrictModeBaselineValidationProblem=true;EnableStrictModeForBaselineValidation=true;PackageValidationBaselinePath={packageValidationBaselinePath}");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("error CP0002: Member 'PackageValidationTestProject.Program.SomeApiOnlyInLatestVersion()' exists on lib/net6.0/PackageValidationTestProject.dll but not on [Baseline] lib/net6.0/PackageValidationTestProject.dll", result.StdOut);
            Assert.Contains("error CP0002: Member 'PackageValidationTestProject.Program.SomeApiOnlyInLatestVersion()' exists on lib/netstandard2.0/PackageValidationTestProject.dll but not on [Baseline] lib/netstandard2.0/PackageValidationTestProject.dll", result.StdOut);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901", Skip = "https://github.com/dotnet/sdk/issues/23533")]
        public void ValidatePackageTargetSucceedsWithBaselineVersionNotInStrictMode()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("PackageValidationTestProject", allowCopyIfPresent: true)
                .WithSource();

            var result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageOutputPath={testAsset.TestRoot}");

            Assert.Equal(0, result.ExitCode);

            string packageValidationBaselinePath = Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.1.0.0.nupkg");
            result = new PackCommand(Log, Path.Combine(testAsset.TestRoot, "PackageValidationTestProject.csproj"))
                .Execute($"-p:PackageVersion=2.0.0;ForceStrictModeBaselineValidationProblem=true;PackageValidationBaselinePath={packageValidationBaselinePath}");

            Assert.Equal(0, result.ExitCode);
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
