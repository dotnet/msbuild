using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;
using System.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class ConstantStringValues
    {
        public static string IdentifierDirectoryPrefix = "Nuget_reference_compat";
        public static string ReferencerBaseDirectory = "Reference";
        public static string NuGetSharedDirectoryIdentifierPostfix = "_NuGetDependencies";
        public static string NetstandardToken = "netstandard";
        public static string DependencyPrefix = "D_";
    }

    public class GivenThatWeWantToVerifyNuGetReferenceCompatFixture : IDisposable
    {
        public GivenThatWeWantToVerifyNuGetReferenceCompatFixture()
        {
            DeleteNuGetArtifacts();
        }

        public void Dispose()
        {
            DeleteNuGetArtifacts();
        }

        private void DeleteNuGetArtifacts()
        {
            try
            {
                //  Delete the shared NuGet package directory before running all the tests.
                string directory = Path.Combine(RepoInfo.GetBaseDirectory(), ConstantStringValues.IdentifierDirectoryPrefix + ConstantStringValues.NuGetSharedDirectoryIdentifierPostfix);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
                //  Delete the generated NuGet packages in the cache.
                foreach (string dir in Directory.EnumerateDirectories(RepoInfo.NuGetCachePath, ConstantStringValues.DependencyPrefix + "*"))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // No-Op; as this is a precaution - do not throw an exception.
            }
        }
    }

    [CollectionDefinition("GivenThatWeWantToVerifyNuGetReferenceCompat Fixture Collection")]
    public class GivenThatWeWantToVerifyNuGetReferenceCompatFixtureCollection : ICollectionFixture<GivenThatWeWantToVerifyNuGetReferenceCompatFixture>
    {

    }

    [Collection("GivenThatWeWantToVerifyNuGetReferenceCompat Fixture Collection")]
    public class GivenThatWeWantToVerifyNuGetReferenceCompatFixtureCollectionClass
    {
        GivenThatWeWantToVerifyNuGetReferenceCompatFixtureCollection fixture;

        public GivenThatWeWantToVerifyNuGetReferenceCompatFixtureCollectionClass(GivenThatWeWantToVerifyNuGetReferenceCompatFixtureCollection fixture)
        {
            this.fixture = fixture;
        }
    }

    public class GivenThatWeWantToVerifyNuGetReferenceCompat : IClassFixture<GivenThatWeWantToVerifyNuGetReferenceCompatFixture>
    {
        [Theory]
        [InlineData("net45", "Full", "netstandard1.0 netstandard1.1 net45", true, true)]
        [InlineData("net451", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 net45 net451", true, true)]
        [InlineData("net46", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 net45 net451 net46", true, true)]
        [InlineData("net461", "PartM3", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 net45 net451 net46 net461", true, true)]
        [InlineData("net462", "PartM2", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 net45 net451 net46 net461", true, true)]
        //  Fullframework NuGet versioning on Jenkins infrastructure issue
        //        https://github.com/dotnet/sdk/issues/1041
        //[InlineData("net461", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]
        //[InlineData("net462", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 net45 net451 net46 net461", true, true)]

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
        [InlineData("netcoreapp2.0", "PartM1", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]
        //  Fullframework NuGet versioning on Jenkins infrastructure issue
        //        https://github.com/dotnet/sdk/issues/1041
        //[InlineData("netcoreapp2.0", "Full", "netstandard1.0 netstandard1.1 netstandard1.2 netstandard1.3 netstandard1.4 netstandard1.5 netstandard1.6 netstandard2.0 netcoreapp1.0 netcoreapp1.1 netcoreapp2.0", true, true)]

        //  OptIn matrix throws an exception for each permutation
        //[InlineData("netstandard2.0", "OptIn", "net45 net451 net46 net461", true, true)]
        //[InlineData("netcoreapp2.0", "OptIn", "net45 net451 net46 net461", true, true)]

        public void Nuget_reference_compat(string referencerTarget, string testDescription, string rawDependencyTargets,
                bool restoreSucceeds, bool buildSucceeds)
        {
            SdkTest sdkTest = new SdkTest();
            TestAssetsManager testAssetsManager = new TestAssetsManager();

            string referencerDirectoryIdentifierPostfix = "_" + referencerTarget + "_" + testDescription;

            TestProject referencerProject = GetTestProject(ConstantStringValues.ReferencerBaseDirectory, referencerTarget, true);

            foreach (string dependencyTarget in rawDependencyTargets.Split(',', ';', ' ').ToList())
            {
                TestProject dependencyProject = GetTestProject(ConstantStringValues.DependencyPrefix + dependencyTarget.Replace('.', '_'), dependencyTarget, true);
                dependencyProject.PublishedNuGetPackageLibrary = new PackageReference(dependencyProject.Name, "1.0.0", ConstructNuGetPackageReferencePath(dependencyProject));
                referencerProject.ReferencedProjects.Add(dependencyProject);
            }

            //  Skip running .NET Framework tests if not running on Windows
            //        https://github.com/dotnet/sdk/issues/335
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!AllProjectsBuildOnNonWindows(referencerProject))
                {
                    return;
                }
            }

            //  Set the referencer project as an Exe unless it targets .NET Standard
            if (!referencerProject.ShortTargetFrameworkIdentifiers.Contains(ConstantStringValues.NetstandardToken))
            {
                referencerProject.IsExe = true;
            }

            //  Create the NuGet packages;
            //        do not create a NuGet package if it was created by a previous permutation.
            foreach (TestProject dependencyProject in referencerProject.ReferencedProjects)
            {
                if (!NuGetPackageExists(dependencyProject))
                {
                    var dependencyTestAsset = testAssetsManager.CreateTestProject(dependencyProject, ConstantStringValues.IdentifierDirectoryPrefix, ConstantStringValues.NuGetSharedDirectoryIdentifierPostfix);
                    var dependencyRestoreCommand = dependencyTestAsset.GetRestoreCommand(relativePath: dependencyProject.Name).Execute().Should().Pass();
                    var dependencyProjectDirectory = Path.Combine(dependencyTestAsset.TestRoot, dependencyProject.Name);

                    var dependencyBuildCommand = new BuildCommand(Stage0MSBuild, dependencyProjectDirectory);
                    var dependencyBuildResult = dependencyBuildCommand.Execute().Should().Pass();

                    var dependencyPackCommand = new PackCommand(Stage0MSBuild, dependencyProjectDirectory);
                    var dependencyPackResult = dependencyPackCommand.Execute().Should().Pass();
                }
            }

            //  Create the referencing app and run the compat test
            var referencerTestAsset = testAssetsManager.CreateTestProject(referencerProject, ConstantStringValues.IdentifierDirectoryPrefix, referencerDirectoryIdentifierPostfix);
            var referencerRestoreCommand = referencerTestAsset.GetRestoreCommand(relativePath: referencerProject.Name);

            //  Modify the restore command to refer to the NuGet packages just created
            foreach (TestProject dependencyProject in referencerProject.ReferencedProjects)
            {
                referencerRestoreCommand.AddSource(dependencyProject.PublishedNuGetPackageLibrary.LocalPath);
            }

            if (restoreSucceeds)
            {
                referencerRestoreCommand.Execute().Should().Pass();
            }
            else
            {
                referencerRestoreCommand.CaptureStdOut().Execute().Should().Fail();
            }

            var referencerBuildCommand = new BuildCommand(Stage0MSBuild, Path.Combine(referencerTestAsset.TestRoot, referencerProject.Name));
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

        TestProject GetTestProject(string name, string target, bool isSdkProject)
        {
            TestProject ret = new TestProject()
            {
                Name = name,
                IsSdkProject = isSdkProject
            };

            if (isSdkProject)
            {
                ret.TargetFrameworks = target;
                //  Workaround for .NET Core 2.0
                if (target.Equals("netcoreapp2.0", StringComparison.OrdinalIgnoreCase))
                {
                    ret.RuntimeFrameworkVersion = RepoInfo.NetCoreApp20Version;
                }
            }
            else
            {
                ret.TargetFrameworkVersion = target;
            }

            return ret;
        }

        bool AllProjectsBuildOnNonWindows(TestProject referencerProject)
        {
            return (referencerProject.BuildsOnNonWindows && referencerProject.ReferencedProjects.All(rp => rp.BuildsOnNonWindows));
        }

        bool NuGetPackageExists(TestProject dependencyProject)
        {
            return File.Exists(Path.Combine(dependencyProject.PublishedNuGetPackageLibrary.LocalPath,
                    String.Concat(dependencyProject.PublishedNuGetPackageLibrary.ID + "." + dependencyProject.PublishedNuGetPackageLibrary.Version + ".nupkg"))) ;
        }

        string ConstructNuGetPackageReferencePath(TestProject dependencyProject)
        {
            return Path.Combine(RepoInfo.GetBaseDirectory(), ConstantStringValues.IdentifierDirectoryPrefix + ConstantStringValues.NuGetSharedDirectoryIdentifierPostfix, dependencyProject.Name, dependencyProject.Name, "bin", "Debug");
        }

    }
}
