// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.NET.Pack.Tests
{
    public class GivenThatWeWantToPackAHelloWorldProject : SdkTest
    {
        public GivenThatWeWantToPackAHelloWorldProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_packs_successfully()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "PackHelloWorld")
                .WithSource();

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand
                .Execute()
                .Should()
                .Pass();

            //  Validate the contents of the NuGet package by looking at the generated .nuspec file, as that's simpler
            //  than unzipping and inspecting the .nupkg
            string nuspecPath = packCommand.GetIntermediateNuspecPath();
            var nuspec = XDocument.Load(nuspecPath);

            var ns = nuspec.Root.Name.Namespace;
            XElement filesSection = nuspec.Root.Element(ns + "files");

            var fileTargets = filesSection.Elements().Select(files => files.Attribute("target").Value).ToList();

            var expectedFileTargets = new[]
            {
                $@"lib\{ToolsetInfo.CurrentTargetFramework}\HelloWorld.runtimeconfig.json",
                $@"lib\{ToolsetInfo.CurrentTargetFramework}\HelloWorld.dll"
            }.Select(p => p.Replace('\\', Path.DirectorySeparatorChar));

            fileTargets.Should().BeEquivalentTo(expectedFileTargets);
        }

        [Fact]
        public void It_fails_if_nobuild_was_requested_but_build_was_invoked()
        {
            var testProject = new TestProject()
            {
                Name = "InvokeBuildOnPack",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    project.Root.Add(XElement.Parse(@"<Target Name=""InvokeBuild"" DependsOnTargets=""Build"" BeforeTargets=""Pack"" />"));
                });

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new PackCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute("/p:NoBuild=true")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1085");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_packs_with_release_if_PackRelease_property_set(bool optedOut)
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld", identifier: optedOut.ToString())
               .WithSource();

            File.WriteAllText(Path.Combine(helloWorldAsset.Path, "Directory.Build.props"), "<Project><PropertyGroup><PackRelease>true</PackRelease></PropertyGroup></Project>");

            var packCommand = new DotnetPackCommand(Log, helloWorldAsset.TestRoot);

            packCommand
                .WithEnvironmentVariable(EnvironmentVariableNames.DISABLE_PUBLISH_AND_PACK_RELEASE, optedOut.ToString())
                .Execute()
                .Should()
                .Pass();

            var expectedAssetPath = Path.Combine(helloWorldAsset.Path, "bin", optedOut ? "Debug" : "Release", "HelloWorld.1.0.0.nupkg");
            Assert.True(File.Exists(expectedAssetPath));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public void It_packs_with_release_if_PackRelease_property_set_in_csproj(string valueOfPackRelease)
        {
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld")
               .WithSource()
               .WithProjectChanges(project =>
               {
                   var ns = project.Root.Name.Namespace;
                   var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                   propertyGroup.Add(new XElement(ns + "PackRelease", valueOfPackRelease));
               });

            var packCommand = new DotnetPackCommand(Log, helloWorldAsset.TestRoot);

            packCommand
                .Execute()
                .Should()
                .Pass();

            var expectedAssetPath = Path.Combine(helloWorldAsset.Path, "bin", valueOfPackRelease == "true" ? "Release" : "Debug", "HelloWorld.1.0.0.nupkg");
            new FileInfo(expectedAssetPath).Should().Exist();
        }

        [InlineData("")]
        [InlineData("false")]
        [Theory]
        public void It_packs_successfully_with_Multitargeting_where_net_8_and_net_7_project_defines_PackRelease_or_not(string packReleaseValue)
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: packReleaseValue)
                .WithSource()
                .WithTargetFrameworks("net8.0;net7.0")
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    if (packReleaseValue != "")
                    {
                        propertyGroup
                            .Add(new XElement(ns + "PackRelease", packReleaseValue));
                    };
                });

            var packCommand = new DotnetPackCommand(Log, helloWorldAsset.TestRoot);

            packCommand
                .Execute()
                .Should()
                .Pass();

            string expectedConfiguration = packReleaseValue == "false" ? "Debug" : "Release";
            var expectedAssetPath = Path.Combine(helloWorldAsset.Path, "bin", expectedConfiguration, "HelloWorld.1.0.0.nupkg");
            new FileInfo(expectedAssetPath).Should().Exist();
        }

        [Fact]
        public void A_PackRelease_property_does_not_affect_other_commands_besides_pack()
        {
            var tfm = "net8.0";
            var helloWorldAsset = _testAssetsManager
               .CopyTestAsset("HelloWorld")
               .WithSource()
               .WithTargetFramework(tfm);

            File.WriteAllText(helloWorldAsset.Path + "/Directory.Build.props", "<Project><PropertyGroup><PackRelease>false</PackRelease></PropertyGroup></Project>");

            var publishCommand = new DotnetPublishCommand(Log, helloWorldAsset.TestRoot);

            publishCommand
                .Execute()
                .Should()
                .Pass();

            var unexpectedAssetPath = Path.Combine(helloWorldAsset.Path, "bin", "Debug", tfm, "HelloWorld.dll");
            Assert.False(File.Exists(unexpectedAssetPath));
            var expectedAssetPath = Path.Combine(helloWorldAsset.Path, "bin", "Release", tfm, "HelloWorld.dll");
            Assert.True(File.Exists(expectedAssetPath));
        }
    }
}
