// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExeWithFSharp : SdkTest
    {
        public GivenThatWeWantToBuildADesktopExeWithFSharp(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_builds_a_simple_desktop_app()
        {
            var targetFramework = "net45";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorldFS")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Element(ns + "TargetFramework").SetValue(targetFramework);
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.exe",
                "TestApp.exe.config",
                "TestApp.pdb",
                "FSharp.Core.dll",
                "System.ValueTuple.dll",
            });
        }

        [WindowsOnlyFact]
        public void It_builds_a_simple_net50_app()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorldFS")
                .WithSource()
                .WithTargetFramework("net5.0");

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
