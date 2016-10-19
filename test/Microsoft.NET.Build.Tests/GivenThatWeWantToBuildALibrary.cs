// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibrary
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

       [Fact]
        public void It_builds_the_library_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .Restore(relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.5");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json"
            });
        }

       [Fact]
        public void It_builds_the_library_twice_in_a_row()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .Restore(relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData(".NETStandard,Version=v1.0", new[] { "NETSTANDARD1_0" }, false)]
        [InlineData("netstandard1.3", new[] { "NETSTANDARD1_3" }, false)]
        [InlineData("netstandard1.6", new[] { "NETSTANDARD1_6" }, false)]
        [InlineData("netstandard20", new[] { "NETSTANDARD2_0" }, false)]
        [InlineData("net45", new[] { "NET45" }, true)]
        [InlineData("net461", new[] { "NET461" }, true)]
        [InlineData("netcoreapp1.0", new[] { "NETCOREAPP1_0" }, false)]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile78", new string[] { }, false)]
        [InlineData(".NETFramework,Version=v4.0,Profile=Client", new string[] { "NET40" }, false)]
        [InlineData("Xamarin.iOS,Version=v1.0", new string[] { "XAMARINIOS1_0" }, false)]
        [InlineData("UnknownFramework,Version=v3.14", new string[] { "UNKNOWNFRAMEWORK3_14" }, false)]
        public void It_implicitly_defines_compilation_constants_for_the_target_framework(string targetFramework, string[] expectedDefines, bool buildOnlyOnWindows)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary", "ImplicitFrameworkConstants", targetFramework)
                .WithSource();

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);

            //  Update target framework in project
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var project = XDocument.Load(buildCommand.FullPathProjectFile);

            var targetFrameworkProperties = project.Root
                .Elements(ns + "PropertyGroup")
                .Elements(ns + "TargetFramework")
                .ToList();

            targetFrameworkProperties.Count.Should().Be(1);

            bool shouldCompile;

            if (targetFramework.Contains(",Version="))
            {
                //  We use the full TFM for frameworks we don't have built-in support for targeting, so we don't want to run the Compile target
                shouldCompile = false;

                var frameworkName = new FrameworkName(targetFramework);

                var targetFrameworkProperty = targetFrameworkProperties.Single();
                targetFrameworkProperty.AddBeforeSelf(new XElement(ns + "TargetFrameworkIdentifier", frameworkName.Identifier));
                targetFrameworkProperty.AddBeforeSelf(new XElement(ns + "TargetFrameworkVersion", "v" + frameworkName.Version.ToString()));
                if (!string.IsNullOrEmpty(frameworkName.Profile))
                {
                    targetFrameworkProperty.AddBeforeSelf(new XElement(ns + "TargetFrameworkProfile", frameworkName.Profile));
                }

                //  For the NuGet restore task to work with package references, it needs the TargetFramework property to be set.
                //  Otherwise we would just remove the property.
                targetFrameworkProperty.SetValue(targetFramework);
            }
            else
            {
                shouldCompile = true;
                targetFrameworkProperties.Single().SetValue(targetFramework);
            }

            if (buildOnlyOnWindows && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shouldCompile = false;
            }

            using (var file = File.CreateText(buildCommand.FullPathProjectFile))
            {
                project.Save(file);
            }

            testAsset.Restore(relativePath: "TestLibrary");

            //  Override build target to write out DefineConstants value to a file in the output directory
            Directory.CreateDirectory(buildCommand.GetBaseIntermediateDirectory().FullName);
            string injectTargetPath = Path.Combine(
                buildCommand.GetBaseIntermediateDirectory().FullName,
                Path.GetFileName(buildCommand.ProjectFile) + ".WriteDefinedConstants.g.targets");

            string injectTargetContents =
@"<Project ToolsVersion=`14.0` xmlns=`http://schemas.microsoft.com/developer/msbuild/2003`>
  <Target Name=`Build` " + (shouldCompile ? "DependsOnTargets=`Compile`" : "") + @">
    <WriteLinesToFile
      File=`$(OutputPath)\DefinedConstants.txt`
      Lines=`$(DefineConstants)`
      Overwrite=`true`
      Encoding=`Unicode`
      />
  </Target>
</Project>";
            injectTargetContents = injectTargetContents.Replace('`', '"');

            File.WriteAllText(injectTargetPath, injectTargetContents);

            //  Build project
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            outputDirectory.Create();

            buildCommand
                .Execute()
                .Should()
                .Pass();

            //  Verify expected DefineConstants
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "DefinedConstants.txt",
            });

            var definedConstants = File.ReadAllLines(Path.Combine(outputDirectory.FullName, "DefinedConstants.txt"))
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();

            definedConstants.Should().BeEquivalentTo(new[] { "DEBUG", "TRACE" }.Concat(expectedDefines).ToArray());
        }
    }
}