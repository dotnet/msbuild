// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToVerifyNuGetReferenceCompat : SdkTest, IClassFixture<DeleteNuGetArtifactsFixture>
    {
        public GivenThatWeWantToVerifyNuGetReferenceCompat(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("net45", "Full", "netstandard1.0 netstandard1.1 net45", true, true)]
        [InlineData("net451", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 net45 net451", true, true)]
        [InlineData("net46", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 net45 net451 net46", true, true)]
        [InlineData("net461", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]
        [InlineData("net462", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461 net462", true, true)]
        [InlineData("netstandard1.0", "Full", "netstandard1.0", true, true)]
        [InlineData("netstandard1.1", "Full", "netstandard1.0 netstandard1.1", true, true)]
        [InlineData("netstandard1.2", "Full", "netstandard1.0 netstandard1.1 netstandard1.2", true, true)]
        [InlineData("netstandard1.3", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3", true, true)]
        [InlineData("netstandard1.4", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4", true, true)]
        [InlineData("netstandard1.5", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5", true, true)]
        [InlineData("netstandard1.6", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6", true, true)]
        [InlineData("netstandard2.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0", true, true)]
        [InlineData("netcoreapp1.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0", true, true)]
        [InlineData("netcoreapp1.1", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0 netcoreapp1.1", true, true)]
        [InlineData("netcoreapp2.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]

        [InlineData("netstandard2.0", "OptIn", "net45 net451 net46 net461", true, true)]
        [InlineData("netcoreapp2.0", "OptIn", "net45 net451 net46 net461", true, true)]

        public void Nuget_reference_compat(string referencerTarget, string testDescription, string rawDependencyTargets,
                bool restoreSucceeds, bool buildSucceeds)
        {
            string referencerDirectoryNamePostfix = "_" + referencerTarget + "_" + testDescription;

            TestProject referencerProject = GetTestProject(ConstantStringValues.ReferencerDirectoryName, referencerTarget, true);

            //  Skip running test if not running on Windows
            //        https://github.com/dotnet/sdk/issues/335
            if (!(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || referencerProject.BuildsOnNonWindows))
            {
                return;
            }

            foreach (string dependencyTarget in rawDependencyTargets.Split(',', ';', ' ').ToList())
            {
                TestProject dependencyProject = GetTestProject(ConstantStringValues.DependencyDirectoryNamePrefix + dependencyTarget.Replace('.', '_'), dependencyTarget, true);
                TestPackageReference dependencyPackageReference = new(
                    dependencyProject.Name,
                    "1.0.0",
                    ConstantStringValues.ConstructNuGetPackageReferencePath(dependencyProject, identifier: referencerTarget + testDescription + rawDependencyTargets));

                //  Skip creating the NuGet package if not running on Windows; or if the NuGet package already exists
                //        https://github.com/dotnet/sdk/issues/335
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || dependencyProject.BuildsOnNonWindows)
                {
                    if (!dependencyPackageReference.NuGetPackageExists())
                    {
                        //  Create the NuGet packages
                        var dependencyTestAsset = _testAssetsManager.CreateTestProject(dependencyProject, identifier: referencerTarget + testDescription + rawDependencyTargets);
                        var dependencyRestoreCommand = dependencyTestAsset.GetRestoreCommand(Log, relativePath: dependencyProject.Name).Execute().Should().Pass();
                        var dependencyProjectDirectory = Path.Combine(dependencyTestAsset.TestRoot, dependencyProject.Name);

                        var dependencyPackCommand = new PackCommand(Log, dependencyProjectDirectory);
                        var dependencyPackResult = dependencyPackCommand.Execute().Should().Pass();
                    }

                    referencerProject.PackageReferences.Add(dependencyPackageReference);
                }
            }

            //  Skip running tests if no NuGet packages are referenced
            //        https://github.com/dotnet/sdk/issues/335
            if (referencerProject.PackageReferences == null)
            {
                return;
            }

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.TargetFrameworkIdentifiers.Contains(ConstantStringValues.NetstandardTargetFrameworkIdentifier))
            {
                referencerProject.IsExe = true;
            }

            //  Create the referencing app and run the compat test
            var referencerTestAsset = _testAssetsManager.CreateTestProject(referencerProject, ConstantStringValues.TestDirectoriesNamePrefix, referencerDirectoryNamePostfix);
            var referencerRestoreCommand = referencerTestAsset.GetRestoreCommand(Log, relativePath: referencerProject.Name);

            List<string> referencerRestoreSources = new();

            //  Modify the restore command to refer to the created NuGet packages
            foreach (TestPackageReference packageReference in referencerProject.PackageReferences)
            {
                var source = Path.Combine(packageReference.NupkgPath, packageReference.ID, "bin", "Debug");
                referencerRestoreSources.Add(source);
            }

            NuGetConfigWriter.Write(referencerTestAsset.TestRoot, referencerRestoreSources);

            if (restoreSucceeds)
            {
                referencerRestoreCommand.Execute().Should().Pass();
            }
            else
            {
                referencerRestoreCommand.Execute().Should().Fail();
            }

            var referencerBuildCommand = new BuildCommand(referencerTestAsset);
            var referencerBuildResult = referencerBuildCommand.Execute();

            if (buildSucceeds)
            {
                referencerBuildResult.Should().Pass();
            }
            else
            {
                referencerBuildResult.Should().Fail().And.HaveStdOutContaining("It cannot be referenced by a project that targets");
            }
        }

        [WindowsOnlyTheory]
        [InlineData("netstandard2.0")]
        [InlineData("netcoreapp2.0")]
        public void Netfx_is_implicit_for_Netstandard_and_Netcore_20(string targetFramework)
        {
            var testProjectName = targetFramework.Replace(".", "_") + "implicit_atf";

            var (testProjectTestAsset, testPackageReference) = CreateTestAsset(testProjectName, targetFramework, "net461", identifer: targetFramework);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);

            var source = Path.Combine(testPackageReference.NupkgPath, testPackageReference.ID, "bin", "Debug");
            NuGetConfigWriter.Write(testProjectTestAsset.TestRoot, source);

            restoreCommand.Execute().Should().Pass();

            var buildCommand = new BuildCommand(testProjectTestAsset);
            buildCommand.Execute().Should().Pass();
        }

        [WindowsOnlyTheory]
        [InlineData("netstandard1.6")]
        [InlineData("netcoreapp1.1")]
        public void Netfx_is_not_implicit_for_Netstandard_and_Netcore_less_than_20(string targetFramework)
        {
            var testProjectName = targetFramework.Replace(".", "_") + "non_implicit_atf";

            var (testProjectTestAsset, testPackageReference) = CreateTestAsset(testProjectName, targetFramework, "net461", identifer: targetFramework);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            NuGetConfigWriter.Write(testProjectTestAsset.TestRoot, Path.GetDirectoryName(testPackageReference.NupkgPath));
            restoreCommand.Execute().Should().Fail();
        }

        [WindowsOnlyFact]
        public void It_is_possible_to_disable_netfx_implicit_asset_target_fallback()
        {
            const string testProjectName = "netstandard20_disabled_atf";

            var (testProjectTestAsset, testPackageReference) = CreateTestAsset(
                testProjectName,
                "netstandard2.0",
                "net461",
                new Dictionary<string, string> { { "DisableImplicitAssetTargetFallback", "true" } });

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            NuGetConfigWriter.Write(testProjectTestAsset.TestRoot, Path.GetDirectoryName(testPackageReference.NupkgPath));
            restoreCommand.Execute().Should().Fail();
        }

        [WindowsOnlyFact]
        public void It_chooses_lowest_netfx_in_default_atf()
        {
            var testProjectName = $"{ToolsetInfo.CurrentTargetFramework.Replace(".", "")}_multiple_atf";

            var (testProjectTestAsset, testPackageReference) = CreateTestAsset(
               testProjectName,
               ToolsetInfo.CurrentTargetFramework,
               "net462;net472",
               new Dictionary<string, string> { ["CopyLocalLockFileAssemblies"] = "true" });


            var source = Path.Combine(testPackageReference.NupkgPath, testPackageReference.ID, "bin", "Debug");
            NuGetConfigWriter.Write(testProjectTestAsset.TestRoot, source);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute().Should().Pass();

            var buildCommand = new BuildCommand(testProjectTestAsset);
            buildCommand.Execute().Should().Pass();

            var referencedDll = buildCommand.GetOutputDirectory().File("net462_net472_pkg.dll").FullName;
            var referencedTargetFramework = AssemblyInfo.Get(referencedDll)["TargetFrameworkAttribute"];
            referencedTargetFramework.Should().Be(".NETFramework,Version=v4.6.2");
        }

        private (TestAsset, TestPackageReference) CreateTestAsset(
            string testProjectName,
            string callerTargetFramework,
            string calleeTargetFrameworks,
            Dictionary<string, string> additionalProperties = null,
            [CallerMemberName] string testName = null,
            string identifer = null)
        {
            var testPackageReference = CreateTestPackage(calleeTargetFrameworks, testName, identifer);

            var testProject =
                new TestProject
                {
                    Name = testProjectName,
                    TargetFrameworks = callerTargetFramework,
                };

            if (additionalProperties != null)
            {
                foreach (var additionalProperty in additionalProperties)
                {
                    testProject.AdditionalProperties.Add(additionalProperty.Key, additionalProperty.Value);
                }
            }

            testProject.PackageReferences.Add(testPackageReference);

            var testProjectTestAsset = _testAssetsManager.CreateTestProject(
                testProject,
                string.Empty,
                $"{testProjectName}_{calleeTargetFrameworks}");

            return (testProjectTestAsset, testPackageReference);
        }

        private TestPackageReference CreateTestPackage(string targetFrameworks, string identifier, [CallerMemberName] string callingMethod = "")
        {
            var project =
                new TestProject
                {
                    Name = $"{targetFrameworks.Replace(';', '_')}_pkg",
                    TargetFrameworks = targetFrameworks,
                };

            var packageReference =
                new TestPackageReference(
                    project.Name,
                    "1.0.0",
                    ConstantStringValues.ConstructNuGetPackageReferencePath(project, identifier, callingMethod));

            if (!packageReference.NuGetPackageExists())
            {
                var testAsset =
                    _testAssetsManager.CreateTestProject(
                        project,
                        callingMethod,
                        identifier);
                var packageRestoreCommand =
                    testAsset.GetRestoreCommand(Log, relativePath: project.Name).Execute().Should().Pass();
                var dependencyProjectDirectory = Path.Combine(testAsset.TestRoot, project.Name);
                var packagePackCommand =
                    new PackCommand(Log, dependencyProjectDirectory).Execute().Should().Pass();
            }

            return packageReference;
        }

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }
    }
}
