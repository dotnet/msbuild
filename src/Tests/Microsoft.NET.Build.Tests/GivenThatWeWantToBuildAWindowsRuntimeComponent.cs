// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAWindowsRuntimeComponent : SdkTest
    {
        public GivenThatWeWantToBuildAWindowsRuntimeComponent(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_fails_to_produce_winmds_for_net5_0_or_newer()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("WindowsRuntimeComponent")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1131: ");
        }

        [Fact]
        public void It_fails_when_referencing_windows_sdk_contracts_nuget_package_for_net5_0_or_newer()
        {
            var testProject = new TestProject("WinMDClasslibrary")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Windows.Sdk.Contracts", "10.0.18362.2005"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1130: ")
                .And.HaveStdOutContaining("Windows.Foundation.FoundationContract.winmd")
                .And.HaveStdOutContaining("Windows.Foundation.UniversalApiContract.winmd")
                .And.NotHaveStdOutContaining("NETSDK1149");
        }

        [Fact]
        public void It_fails_when_referencing_a_library_using_built_in_winrt_support()
        {
            var testProject = new TestProject("WinMDClasslibrary")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Toolkit.Uwp.Notifications", "6.1.1"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1149: ")
                .And.HaveStdOutContaining("Microsoft.Toolkit.Uwp.Notifications.dll")
                .And.NotHaveStdOutContaining("NETSDK1130");
        }

        [Theory]
        [InlineData("netcoreapp3.1")]
        [InlineData("net48")]
        public void It_successfully_builds_when_referencing_winmds(string targetFramework)
        {
            var testProject = new TestProject("WinMDClasslibrary")
            {
                TargetFrameworks = targetFramework
            };
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Windows.Sdk.Contracts", "10.0.18362.2005"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework.ToString());

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void ManagedWinRTComponentCanBeReferenced()
        {
            var managedWinRTComponent = new TestProject()
            {
                Name = "ManagedWinRTComponent",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0",
            };

            managedWinRTComponent.AdditionalProperties.Add("CsWinRTWindowsMetadata", "10.0.19041.0");
            managedWinRTComponent.AdditionalProperties.Add("CsWinRTComponent", "true");
            managedWinRTComponent.AdditionalProperties.Add("PlatformTarget", "x64");

            //  TODO: Update to latest (currently 1.2.5) once it shows up on dotnet-public feed
            managedWinRTComponent.PackageReferences.Add(new TestPackageReference("Microsoft.Windows.CsWinRT", "1.2.3"));

            managedWinRTComponent.SourceFiles["Coords.cs"] = @"using System;

namespace ManagedWinRTComponent
{
    public sealed class Coord
    {
        public double X;
        public double Y;

        public Coord()
        {
            X = 0.0;
            Y = 0.0;
        }

        public Coord(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Distance(Coord dest)
        {
            double deltaX = (this.X - dest.X);
            double deltaY = (this.Y - dest.Y);
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public override string ToString()
        {
            return ""("" + this.X + "", "" + this.Y + "")"";
        }
    }
}
";

            var consoleApp = new TestProject()
            {
                Name = "ConsoleApp",
                IsExe = true,
                TargetFrameworks = managedWinRTComponent.TargetFrameworks
            };

            consoleApp.AdditionalProperties["PlatformTarget"] = "x64";

            consoleApp.ReferencedProjects.Add(managedWinRTComponent);

            consoleApp.SourceFiles["Program.cs"] = @"
using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(new ManagedWinRTComponent.Coord().ToString());
    }
}";

            var testAsset = _testAssetsManager.CreateTestProject(consoleApp);

            //  Disable workaround for NETSDK1130 which is in Microsoft.Windows.CsWinRT
            File.WriteAllText(Path.Combine(testAsset.TestRoot, "Directory.Build.targets"), @"<Project>
  <Target Name=""UpdateResolveableAssembly"" Returns=""@(TargetPathWithTargetPlatformMoniker)""
          AfterTargets=""GetTargetPath"">
    <ItemGroup>
      <TargetPathWithTargetPlatformMoniker Update=""$(TargetDir)$(AssemblyName).winmd"">
        <ResolveableAssembly>true</ResolveableAssembly>
      </TargetPathWithTargetPlatformMoniker>
    </ItemGroup>
  </Target>
</Project>
");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            //  Make sure the app can run successfully
            var exePath = Path.Combine(buildCommand.GetOutputDirectory(consoleApp.TargetFrameworks).FullName, consoleApp.Name + ".exe");
            new RunExeCommand(Log, exePath)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOut("(0, 0)");
        }

        [FullMSBuildOnlyFact]
        public void WinMDInteropProjectCanBeReferenced()
        {

            var projectionProject = new TestProject("SimpleMathProjection")
            {
                TargetFrameworks = "net5.0-windows10.0.19041.0"
            };

            projectionProject.AdditionalProperties["CsWinRTIncludes"] = "SimpleMathComponent";
            projectionProject.AdditionalProperties["CsWinRTGeneratedFilesDir"] = "$(OutDir)";
            projectionProject.AdditionalProperties["CsWinRTWindowsMetadata"] = "10.0.19041.0";

            //  TODO: Update to latest version
            projectionProject.PackageReferences.Add(new TestPackageReference("Microsoft.Windows.CsWinRT", "1.2.3"));

            //  Don't auto-generate a source file
            projectionProject.SourceFiles["Empty.cs"] = "";

            projectionProject.ProjectChanges.Add(project =>
            {
                var ns = project.Root.Name.Namespace;

                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);

                itemGroup.Add(new XElement(ns + "ProjectReference",
                    new XAttribute("Include", @"..\SimpleMathComponent\SimpleMathComponent.vcxproj")));
            });

            var consoleApp = new TestProject("ConsoleApp")
            {
                TargetFrameworks = "net5.0-windows10.0.19041.0",
                IsExe = true
            };

            consoleApp.ReferencedProjects.Add(projectionProject);

            //  Workaround for PrivateAssets
            consoleApp.PackageReferences.Add(new TestPackageReference("Microsoft.Windows.CsWinRT", "1.2.3"));

            consoleApp.SourceFiles["Program.cs"] = @"
using System;

var x = new SimpleMathComponent.SimpleMath();
Console.WriteLine(""Adding 5.5 + 6.5..."");
Console.WriteLine(x.add(5.5, 6.5).ToString());";


            var testAsset = _testAssetsManager.CreateTestProject(consoleApp);

            //  Copy C++ project file which is referenced
            var cppWinMDSourceDirectory = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("CppWinMDComponent"), "SimpleMathComponent");
            var cppWinTargetDirectory = Path.Combine(testAsset.TestRoot, "SimpleMathComponent");
            Directory.CreateDirectory(cppWinTargetDirectory);
            foreach (var file in Directory.GetFiles(cppWinMDSourceDirectory))
            {
                File.Copy(file, Path.Combine(cppWinTargetDirectory, Path.GetFileName(file)));
            }

            new NuGetExeRestoreCommand(Log, cppWinTargetDirectory)
            {
                PackagesDirectory = Path.Combine(testAsset.Path, "packages")
            }
                .Execute()
                .Should()
                .Pass();


            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();
        }
    }
}
