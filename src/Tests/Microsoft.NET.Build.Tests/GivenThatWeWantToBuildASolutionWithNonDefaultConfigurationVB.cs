// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASolutionWithNonDefaultConfigurationVB : SdkTest
    {
        public GivenThatWeWantToBuildASolutionWithNonDefaultConfigurationVB(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("Release With Spaces", "RELEASE_WITH_SPACES")]
        [InlineData("Release-With-Hyphens", "RELEASE_WITH_HYPHENS")]
        [InlineData("Release.With.Dots", "RELEASE_WITH_DOTS")]
        [InlineData("Release.With-A Mix", "RELEASE_WITH_A_MIX")]
        public void Properly_changes_implicit_defines(string configuration, string expected)
        {
            var targetFramework = "netcoreapp1.0";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorldVB", configuration)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Element(ns + "TargetFramework").SetValue(targetFramework);
                    propertyGroup.SetElementValue(ns + "Configurations", configuration);
                });


            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(new[] { "/v:d", $"/p:Configuration={configuration}" })
                .Should().HaveStdOutContaining($"$(ImplicitConfigurationDefine)=\"{expected}\"");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, configuration);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.dev.json",
                "HelloWorld.runtimeconfig.json",
            });
        }
    }
}
