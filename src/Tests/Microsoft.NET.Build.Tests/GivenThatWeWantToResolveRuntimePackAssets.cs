// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToResolveRuntimePackAssets : SdkTest
    {
        public GivenThatWeWantToResolveRuntimePackAssets(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_errors_if_the_runtime_list_is_missing()
        {
            var testProject = new TestProject()
            {
                Name = "MissingRuntimeListProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    project.Root.Add(CreateTestTarget());
                });

            var projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

            var command = new MSBuildCommand(
                Log,
                "TestResolveRuntimePackAssets",
                projectDirectory);

            command
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(
                    string.Format(
                        Strings.RuntimeListNotFound,
                        Path.Combine(projectDirectory, "data", "RuntimeList.xml")));
        }

        [Fact]
        public void It_errors_if_the_runtime_list_has_duplicates()
        {
            var testProject = new TestProject()
            {
                Name = "DuplicateRuntimeListProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    project.Root.Add(CreateTestTarget());
                });

            var projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);

            Directory.CreateDirectory(Path.Combine(projectDirectory, "data"));

            File.WriteAllText(
                Path.Combine(projectDirectory, "data", "RuntimeList.xml"),
@"<FileList Name="".NET Core 3.0"" TargetFrameworkIdentifier="".NETCoreApp"" TargetFrameworkVersion=""3.0"" FrameworkName=""Microsoft.NETCore.App"">
  <File Type=""Native"" Path=""runtimes/linux-arm/native/libclrjit.so"" FileVersion=""0.0.0.0"" />
  <File Type=""Native"" Path=""runtimes/x64_arm/native/libclrjit.so"" FileVersion=""0.0.0.0"" />
</FileList>");

            var command = new MSBuildCommand(
                Log,
                "TestResolveRuntimePackAssets",
                projectDirectory);

            command
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(
                    string.Format(
                        Strings.DuplicateRuntimePackAsset,
                        "libclrjit.so"));
        }

        private static XElement CreateTestTarget()
        {
            return XElement.Parse(@"
<Target Name=""TestResolveRuntimePackAssets"">
  <ItemGroup>
    <TestFrameworkReference Include=""TestFramework"" />

    <TestRuntimePack Include=""TestRuntimePack"">
      <FrameworkName>TestFramework</FrameworkName>
      <RuntimeIdentifier>test-rid</RuntimeIdentifier>
      <PackageDirectory>$(MSBuildProjectDirectory)</PackageDirectory>
      <PackageName>TestRuntimePackPackage</PackageName>
      <PackageVersion>0.1.0</PackageVersion>
      <IsTrimmable>false</IsTrimmable>
    </TestRuntimePack>
  </ItemGroup>

  <ResolveRuntimePackAssets FrameworkReferences=""@(TestFrameworkReference)"" ResolvedRuntimePacks=""@(TestRuntimePack)"">
    <Output TaskParameter=""RuntimePackAssets"" ItemName=""TestRuntimePackAsset"" />
  </ResolveRuntimePackAssets>

</Target>");
        }
    }
}
