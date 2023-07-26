// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildACrossTargetedLibrary : SdkTest
    {
        public GivenThatWeWantToBuildACrossTargetedLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.1.0.60101")]
        public void It_builds_nondesktop_library_successfully_on_all_platforms()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(Path.Combine("CrossTargeting", "NetStandardAndNetCoreApp"))
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = new DirectoryInfo(Path.Combine(buildCommand.ProjectRootPath, "bin", "Debug"));
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ToolsetInfo.CurrentTargetFramework}/NetStandardAndNetCoreApp.dll",
                $"{ToolsetInfo.CurrentTargetFramework}/NetStandardAndNetCoreApp.pdb",
                $"{ToolsetInfo.CurrentTargetFramework}/NetStandardAndNetCoreApp.runtimeconfig.json",
                $"{ToolsetInfo.CurrentTargetFramework}/NetStandardAndNetCoreApp.deps.json",
                $"{ToolsetInfo.CurrentTargetFramework}/Newtonsoft.Json.dll",
                $"{ToolsetInfo.CurrentTargetFramework}/NetStandardAndNetCoreApp.deps.json",
                $"{ToolsetInfo.CurrentTargetFramework}/NetStandardAndNetCoreApp{EnvironmentInfo.ExecutableExtension}",
                "netstandard1.5/NetStandardAndNetCoreApp.dll",
                "netstandard1.5/NetStandardAndNetCoreApp.pdb",
                "netstandard1.5/NetStandardAndNetCoreApp.deps.json"
            });
        }

        [WindowsOnlyFact]
        public void It_builds_desktop_library_successfully_on_windows()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "DesktopAndNetStandard");
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = new DirectoryInfo(Path.Combine(buildCommand.ProjectRootPath, "bin", "Debug"));
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "net40/DesktopAndNetStandard.dll",
                "net40/DesktopAndNetStandard.pdb",
                "net40/Newtonsoft.Json.dll",
                "net40-client/DesktopAndNetStandard.dll",
                "net40-client/DesktopAndNetStandard.pdb",
                "net40-client/Newtonsoft.Json.dll",
                "net45/DesktopAndNetStandard.dll",
                "net45/DesktopAndNetStandard.pdb",
                "net45/Newtonsoft.Json.dll",
                "netstandard1.5/DesktopAndNetStandard.dll",
                "netstandard1.5/DesktopAndNetStandard.pdb",
                "netstandard1.5/DesktopAndNetStandard.deps.json"
            });
        }

        [Theory]
        [InlineData("1", "win7-x86", "win7-x86;win7-x64", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-arm", "win7-x86;linux;WIN7-X86;unix", "osx-10.12", "win8-arm;win8-arm-aot",
            $"win7-x86;win7-x64;{ToolsetInfo.LatestWinRuntimeIdentifier}-arm;linux;unix;osx-10.12;win8-arm;win8-arm-aot")]
        public void It_combines_inner_rids_for_restore(
            string identifier,
            string outerRid,
            string outerRids,
            string firstFrameworkRid,
            string firstFrameworkRids,
            string secondFrameworkRid,
            string secondFrameworkRids,
            string expectedCombination)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(Path.Combine("CrossTargeting", "NetStandardAndNetCoreApp"), identifier: identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();

                    propertyGroup.Add(
                        new XElement(ns + "RuntimeIdentifier", outerRid),
                        new XElement(ns + "RuntimeIdentifiers", outerRids));

                    propertyGroup.AddAfterSelf(
                        new XElement(ns + "PropertyGroup",
                            new XAttribute(ns + "Condition", "'$(TargetFramework)' == 'netstandard1.5'"),
                            new XElement(ns + "RuntimeIdentifier", firstFrameworkRid),
                            new XElement(ns + "RuntimeIdentifiers", firstFrameworkRids)),
                        new XElement(ns + "PropertyGroup",
                            new XAttribute(ns + "Condition", $"'$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"),
                            new XElement(ns + "RuntimeIdentifier", secondFrameworkRid),
                            new XElement(ns + "RuntimeIdentifiers", secondFrameworkRids)));
                });

            var command = new GetValuesCommand(Log, testAsset.TestRoot, "", valueName: "RuntimeIdentifiers");
            command.DependsOnTargets = "GetAllRuntimeIdentifiers";
            command.ExecuteWithoutRestore().Should().Pass();
            command.GetValues().Should().BeEquivalentTo(expectedCombination.Split(';'));
        }

        [Fact]
        public void OutputPathDoesNotHaveDuplicatedBackslashesInOuterBuild()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net7.0"
            };

            testProject.ProjectChanges.Add(xml =>
            {
                var target = """
                <Target Name="GetOutputPath">
                    <WriteLinesToFile File="$(MSBuildProjectDirectory)\OutputPathValue.txt"
                                      Lines="$(OutputPath)"
                                      Overwrite="true" />
                </Target>
                """;

                xml.Root.Add(XElement.Parse(target));
            });

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new MSBuildCommand(testAsset, "GetOutputPath")
                .Execute()
                .Should()
                .Pass();

            string outputPathValue = File.ReadAllText(Path.Combine(testAsset.TestRoot, testProject.Name, "OutputPathValue.txt"));
            outputPathValue.Trim().Should().NotContain("\\\\");
        }
    }
}
