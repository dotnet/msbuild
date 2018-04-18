using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace EndToEnd
{
    public class GivenAspNetAppsResolveImplicitVersions : TestBase
    {
        private const string AspNetTestProject = "TestWebAppSimple";

        [Fact]
        public void PortablePublishWithLatestTFMUsesBundledAspNetCoreAppVersion()
        {
            var _testInstance = TestAssets.Get(AspNetTestProject)
                .CreateInstance(identifier: LatestSupportedAspNetCoreAppVersion)
                .WithSourceFiles();

            string projectDirectory = _testInstance.Root.FullName;
            string projectPath = Path.Combine(projectDirectory, $"{AspNetTestProject}.csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            //  Update TargetFramework to the right version of .NET Core
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework")
                .Value = "netcoreapp" + LatestSupportedAspNetCoreAppVersion;

            project.Save(projectPath);

            //  Get the implicit version
            new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

            var assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
            var assetsFile = new LockFileFormat().Read(assetsFilePath);

            var restoredVersion = GetAspNetCoreAppVersion(assetsFile, portable: true);
            restoredVersion.Should().NotBeNull();

            var bundledVersionPath = Path.Combine(projectDirectory, ".BundledAspNetCoreVersion");
            var bundledVersion = File.ReadAllText(bundledVersionPath).Trim();

            restoredVersion.ToNormalizedString().Should().BeEquivalentTo(bundledVersion,
                "The bundled aspnetcore versions set in Microsoft.NETCoreSdk.BundledVersions.props should be idenitical to the versions set in DependencyVersions.props." +
                "Please update MSBuildExtensions.targets in this repo so these versions match.");
        }

        [Fact]
        public void StandalonePublishWithLatestTFMUsesBundledAspNetCoreAppVersion()
        {
            var _testInstance = TestAssets.Get(AspNetTestProject)
                .CreateInstance(identifier: LatestSupportedAspNetCoreAppVersion)
                .WithSourceFiles();

            string projectDirectory = _testInstance.Root.FullName;
            string projectPath = Path.Combine(projectDirectory, $"{AspNetTestProject}.csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            //  Update TargetFramework to the right version of .NET Core
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework")
                .Value = "netcoreapp" + LatestSupportedAspNetCoreAppVersion;

            var rid = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();

            //  Set RuntimeIdentifier to simulate standalone publish
            project.Root.Element(ns + "PropertyGroup")
                .Add(new XElement(ns + "RuntimeIdentifier", rid));

            project.Save(projectPath);

            //  Get the implicit version
            new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

            var assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
            var assetsFile = new LockFileFormat().Read(assetsFilePath);

            var restoredVersion = GetAspNetCoreAppVersion(assetsFile);
            restoredVersion.Should().NotBeNull();

            var bundledVersionPath = Path.Combine(projectDirectory, ".BundledAspNetCoreVersion");
            var bundledVersion = File.ReadAllText(bundledVersionPath).Trim();

            restoredVersion.ToNormalizedString().Should().BeEquivalentTo(bundledVersion,
                "The bundled aspnetcore versions set in Microsoft.NETCoreSdk.BundledVersions.props should be idenitical to the versions set in DependencyVersions.props." +
                "Please update MSBuildExtensions.targets in this repo so these versions match.");
        }

        [Theory]
        [MemberData(nameof(SupportedAspNetCoreAppVersions))]
        public void ItRollsForwardToTheLatestVersion(string minorVersion)
        {
            var _testInstance = TestAssets.Get(AspNetTestProject)
                .CreateInstance(identifier: minorVersion)
                .WithSourceFiles();

            string projectDirectory = _testInstance.Root.FullName;

            string projectPath = Path.Combine(projectDirectory, $"{AspNetTestProject}.csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            //  Update TargetFramework to the right version of .NET Core
            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "TargetFramework")
                .Value = "netcoreapp" + minorVersion;

            var rid = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();

            //  Set RuntimeIdentifier to opt in to roll-forward behavior
            project.Root.Element(ns + "PropertyGroup")
                .Add(new XElement(ns + "RuntimeIdentifier", rid));

            project.Save(projectPath);

            //  Get the version rolled forward to
            new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

            string assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
            var assetsFile = new LockFileFormat().Read(assetsFilePath);

            var rolledForwardVersion = GetAspNetCoreAppVersion(assetsFile);
            rolledForwardVersion.Should().NotBeNull();

            if (rolledForwardVersion.IsPrerelease)
            {
                //  If this version of .NET Core is still prerelease, then:
                //  - Floating the patch by adding ".*" to the major.minor version won't work, but
                //  - There aren't any patches to roll-forward to, so we skip testing this until the version
                //    leaves prerelease.
                return;
            }

            //  Float the RuntimeFrameworkVersion to get the latest version of the runtime available from feeds
            Directory.Delete(Path.Combine(projectDirectory, "obj"), true);
            project.Root.Element(ns + "PropertyGroup")
                .Add(new XElement(ns + "RuntimeFrameworkVersion", $"{minorVersion}.*"));
            project.Save(projectPath);

            new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

            var floatedAssetsFile = new LockFileFormat().Read(assetsFilePath);

            var floatedVersion = GetAspNetCoreAppVersion(floatedAssetsFile);
            floatedVersion.Should().NotBeNull();

            rolledForwardVersion.ToNormalizedString().Should().BeEquivalentTo(floatedVersion.ToNormalizedString(),
                "the latest patch version properties in Microsoft.NETCoreSdk.BundledVersions.props need to be updated " +
                "(see MSBuildExtensions.targets in this repo)");
        }

        [Fact]
        public void WeCoverLatestAspNetCoreAppRollForward()
        {
            //  Run "dotnet new web", get TargetFramework property, and make sure it's covered in SupportedAspNetCoreAppVersions
            using (DisposableDirectory directory = Temp.CreateDirectory())
            {
                string projectDirectory = directory.Path;

                new NewCommandShim()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute("web --no-restore")
                    .Should().Pass();

                string projectPath = Path.Combine(projectDirectory, Path.GetFileName(projectDirectory) + ".csproj");

                var project = XDocument.Load(projectPath);
                var ns = project.Root.Name.Namespace;

                string targetFramework = project.Root.Element(ns + "PropertyGroup")
                    .Element(ns + "TargetFramework")
                    .Value;

                SupportedAspNetCoreAppVersions.Select(v => $"netcoreapp{v[0]}")
                    .Should().Contain(targetFramework, $"the {nameof(SupportedAspNetCoreAppVersions)} property should include the default version " +
                    "of Microsoft.AspNetCore.App used by the templates created by \"dotnet new web\"");

            }
        }

        private NuGetVersion GetAspNetCoreAppVersion(LockFile lockFile, bool portable = false)
        {
            return lockFile?.Targets?.SingleOrDefault(t => portable || t.RuntimeIdentifier != null)
                ?.Libraries?.SingleOrDefault(l =>
                    string.Compare(l.Name, "Microsoft.AspNetCore.App", StringComparison.CurrentCultureIgnoreCase) == 0)
                ?.Version;
        }

        public static string LatestSupportedAspNetCoreAppVersion = "2.1";

        public static IEnumerable<object[]> SupportedAspNetCoreAppVersions
        {
            get
            {
                yield return new object[] { LatestSupportedAspNetCoreAppVersion };
            }
        }
    }
}
