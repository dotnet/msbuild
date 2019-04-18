using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace EndToEnd
{
    public partial class GivenSelfContainedAppsRollForward : TestBase
    {
        public const string NETCorePackageName = "Microsoft.NETCore.App";
        public const string AspNetCoreAppPackageName = "Microsoft.AspNetCore.App";
        public const string AspNetCoreAllPackageName = "Microsoft.AspNetCore.All";

        [Theory(Skip = "https://github.com/dotnet/cli/issues/10879")]
        //  MemberData is used instead of InlineData here so we can access it in another test to
        //  verify that we are covering the latest release of .NET Core
        [ClassData(typeof(SupportedNetCoreAppVersions))]
        public void ItRollsForwardToTheLatestNetCoreVersion(string minorVersion)
        {
            ItRollsForwardToTheLatestVersion(NETCorePackageName, minorVersion);
        }

        [Theory(Skip = "https://github.com/dotnet/cli/issues/10879")]
        [ClassData(typeof(SupportedAspNetCoreVersions))]
        public void ItRollsForwardToTheLatestAspNetCoreAppVersion(string minorVersion)
        {
            ItRollsForwardToTheLatestVersion(AspNetCoreAppPackageName, minorVersion);
        }

        [Theory(Skip = "https://github.com/dotnet/cli/issues/10879")]
        [ClassData(typeof(SupportedAspNetCoreVersions))]
        public void ItRollsForwardToTheLatestAspNetCoreAllVersion(string minorVersion)
        {
            ItRollsForwardToTheLatestVersion(AspNetCoreAllPackageName, minorVersion);
        }

        public void ItRollsForwardToTheLatestVersion(string packageName, string minorVersion)
        {
            var _testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance(identifier: packageName + "_" + minorVersion)
                .WithSourceFiles();

            string projectDirectory = _testInstance.Root.FullName;

            string projectPath = Path.Combine(projectDirectory, "TestAppSimple.csproj");

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

            if (packageName != NETCorePackageName)
            {
                //  Add implicit ASP.NET reference
                project.Root.Add(new XElement(ns + "ItemGroup",
                    new XElement(ns + "PackageReference", new XAttribute("Include", packageName))));
            }

            project.Save(projectPath);

            //  Get the version rolled forward to
            new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

            string assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");
            var assetsFile = new LockFileFormat().Read(assetsFilePath);

            var rolledForwardVersion = GetPackageVersion(assetsFile, packageName);
            rolledForwardVersion.Should().NotBeNull();

            if (rolledForwardVersion.IsPrerelease)
            {
                //  If this version of .NET Core is still prerelease, then:
                //  - Floating the patch by adding ".*" to the major.minor version won't work, but
                //  - There aren't any patches to roll-forward to, so we skip testing this until the version
                //    leaves prerelease.
                return;
            }

            Directory.Delete(Path.Combine(projectDirectory, "obj"), true);
            if (packageName == NETCorePackageName)
            {
                //  Float the RuntimeFrameworkVersion to get the latest version of the runtime available from feeds
                project.Root.Element(ns + "PropertyGroup")
                    .Add(new XElement(ns + "RuntimeFrameworkVersion", $"{minorVersion}.*"));
            }
            else
            {
                project.Root.Element(ns + "ItemGroup")
                    .Element(ns + "PackageReference")
                    .Add(new XAttribute("Version", $"{minorVersion}.*"),
                        new XAttribute("AllowExplicitVersion", "true"));
            }

            project.Save(projectPath);

            new RestoreCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass();

            var floatedAssetsFile = new LockFileFormat().Read(assetsFilePath);

            var floatedVersion = GetPackageVersion(floatedAssetsFile, packageName);
            floatedVersion.Should().NotBeNull();

            rolledForwardVersion.ToNormalizedString().Should().BeEquivalentTo(floatedVersion.ToNormalizedString(),
                $"the latest patch version for {packageName} {minorVersion} in Microsoft.NETCoreSdk.BundledVersions.props " +
                "needs to be updated (see the ImplicitPackageVariable items in MSBuildExtensions.targets in this repo)");
        }

        private static NuGetVersion GetPackageVersion(LockFile lockFile, string packageName)
        {
            return lockFile?.Targets?.SingleOrDefault(t => t.RuntimeIdentifier != null)
                ?.Libraries?.SingleOrDefault(l =>
                    string.Compare(l.Name, packageName, StringComparison.CurrentCultureIgnoreCase) == 0)
                ?.Version;
        }

        [Fact]
        public void WeCoverLatestNetCoreAppRollForward()
        {
            //  Run "dotnet new console", get TargetFramework property, and make sure it's covered in SupportedNetCoreAppVersions
            using (DisposableDirectory directory = Temp.CreateDirectory())
            {
                string projectDirectory = directory.Path;

                new NewCommandShim()
                    .WithWorkingDirectory(projectDirectory)
                    .Execute("console --no-restore")
                    .Should().Pass();

                string projectPath = Path.Combine(projectDirectory, Path.GetFileName(projectDirectory) + ".csproj");

                var project = XDocument.Load(projectPath);
                var ns = project.Root.Name.Namespace;

                string targetFramework = project.Root.Element(ns + "PropertyGroup")
                    .Element(ns + "TargetFramework")
                    .Value;

                SupportedNetCoreAppVersions.Versions.Select(v => $"netcoreapp{v}")
                    .Should().Contain(targetFramework, $"the {nameof(SupportedNetCoreAppVersions)}.{nameof(SupportedNetCoreAppVersions.Versions)} property should include the default version " +
                    "of .NET Core created by \"dotnet new\"");
                
            }
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

                SupportedAspNetCoreVersions.Versions.Select(v => $"netcoreapp{v}")
                    .Should().Contain(targetFramework, $"the {nameof(SupportedAspNetCoreVersions)} should include the default version " +
                    "of Microsoft.AspNetCore.App used by the templates created by \"dotnet new web\"");

            }
        }
    }
}
