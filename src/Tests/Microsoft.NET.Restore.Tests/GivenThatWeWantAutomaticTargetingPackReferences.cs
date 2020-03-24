using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Utilities;
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
    public class GivenThatWeWantAutomaticTargetingPackReferences : SdkTest
    {
        public GivenThatWeWantAutomaticTargetingPackReferences(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("4.7.1")]
        [InlineData("4.7.2")]
        [InlineData("4.5.1")]
        [InlineData("4.8")]
        public void It_restores_net_framework_project_successfully(string version)
        {
            var targetFrameworkVersion = (TargetDotNetFrameworkVersion)System.Enum.Parse(typeof(TargetDotNetFrameworkVersion), "Version" + string.Join("", version.Split('.')));
            var targetFramework = "net" + string.Join("", version.Split('.'));
            var testProject = new TestProject()
            {
                Name = "ProjectWithoutTargetingPackRef",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                testProject.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProject.Name);
            restoreCommand.Execute().Should().Pass();

            LockFile lockFile = LockFileUtilities.GetLockFile(projectAssetsJsonPath, NullLogger.Instance);
            var netFrameworkLibrary = lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v" + version), null).Libraries.FirstOrDefault((file) => file.Name.Contains(targetFramework));

            if (ReferenceAssembliesAreInstalled(targetFrameworkVersion))
            {
                netFrameworkLibrary.Should().BeNull();
            }
            else
            {
                netFrameworkLibrary.Name.Should().Be("Microsoft.NETFramework.ReferenceAssemblies." + targetFramework);
                netFrameworkLibrary.Type.Should().Be("package");
            }
        }

        [Fact]
        public void It_restores_multitargeted_net_framework_project_successfully()
        {
            var testProject = new TestProject()
            {
                Name = "ProjectWithoutTargetingPackRef",
                TargetFrameworks = "net471;net472;netcoreapp3.0",
                IsSdkProject = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            string projectAssetsJsonPath = Path.Combine(
                testAsset.Path,
                testProject.Name,
                "obj",
                "project.assets.json");

            var restoreCommand =
                testAsset.GetRestoreCommand(Log, relativePath: testProject.Name);
            restoreCommand.Execute().Should().Pass();

            LockFile lockFile = LockFileUtilities.GetLockFile(
                projectAssetsJsonPath,
                NullLogger.Instance);

            var net471FrameworkLibrary = lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v4.7.1"), null).Libraries.FirstOrDefault((file) => file.Name.Contains("net471"));
            if (ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion.Version471))
            {
                net471FrameworkLibrary.Should().BeNull();
            }
            else
            { 
                net471FrameworkLibrary.Name.Should().Be("Microsoft.NETFramework.ReferenceAssemblies.net471");
                net471FrameworkLibrary.Type.Should().Be("package");
            }

            var net472FrameworkLibrary = lockFile.GetTarget(NuGetFramework.Parse(".NETFramework,Version=v4.7.2"), null).Libraries.FirstOrDefault((file) => file.Name.Contains("net472"));

            if (ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion.Version472))
            {
                net472FrameworkLibrary.Should().BeNull();
            }
            else
            {
                net472FrameworkLibrary.Name.Should().Be("Microsoft.NETFramework.ReferenceAssemblies.net472");
                net472FrameworkLibrary.Type.Should().Be("package");
            }

        }

        public static bool ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion targetFrameworkVersion)
        {
            var referenceAssemblies = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(targetFrameworkVersion);
            return referenceAssemblies != null;
        }
    }
}
