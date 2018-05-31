using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToRestoreToUseImplicitPackages : SdkTest
    {
        public GivenThatWeWantToRestoreToUseImplicitPackages(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_uses_NetstandardLibrary20x_as_the_implicit_version_for_NetStandard20()
        {
            const string testProjectName = "NetStandard2Library";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(project)
                .Restore(Log, project.Name);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                project.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute().Should().Pass();

            LockFile lockFile = LockFileUtilities.GetLockFile(
                projectAssetsJsonPath,
                NullLogger.Instance);

            var target =
                lockFile.GetTarget(NuGetFramework.Parse(".NETStandard,Version=v2.0"), null);
            var netStandardLibrary =
                target.Libraries.Single(l => l.Name == "NETStandard.Library");
            netStandardLibrary.Version.ToString().Should().Be("2.0.3");
        }

        [Fact]
        public void It_uses_MicrosoftNETCoreApp20x_as_the_implicit_version_for_NetCoreApp20()
        {
            const string testProjectName = "NetCoreApp2";
            var project = new TestProject
            {
                Name = testProjectName,
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(project)
                .Restore(Log, project.Name);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                project.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute().Should().Pass();

            LockFile lockFile = LockFileUtilities.GetLockFile(
                projectAssetsJsonPath,
                NullLogger.Instance);

            var target =
                lockFile.GetTarget(NuGetFramework.Parse(".NetCoreApp,Version=v2.0"), null);
            var netStandardLibrary =
                target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");
            netStandardLibrary.Version.ToString().Should().Be("2.0.0");
        }
    }
}
