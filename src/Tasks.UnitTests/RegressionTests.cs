// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    public sealed class RegressionTests
    {
        private readonly ITestOutputHelper _output;

        public RegressionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Verifies that when a user overrides the BaseIntermediateOutputPath that the build still works.
        /// </summary>
        /// <remarks>This was written because of regression https://github.com/dotnet/msbuild/issues/1509. </remarks>
        [Fact]
        public void OverrideBaseIntermediateOutputPathSucceeds()
        {
            Project project = ObjectModelHelpers.CreateInMemoryProject($@"
                <Project DefaultTargets=""Build"" xmlns=""msbuildnamespace"" ToolsVersion=""msbuilddefaulttoolsversion"">
                    <Import Project=""$(MSBuildToolsPath)\Microsoft.Common.props"" />

                    <PropertyGroup>
                        <BaseIntermediateOutputPath>obj\x86\Debug</BaseIntermediateOutputPath>
                    </PropertyGroup>

                    <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />

                    <Target Name=""Build"" />
                </Project>
                ");

            bool result = project.Build(new MockLogger(_output));

            Assert.True(result);
        }

        /// <summary>
        /// Tests fix for https://github.com/dotnet/msbuild/issues/1479.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyAttributesLocation()
        {
            var expectedCompileItems = "a.cs;" + Path.Combine("obj", "Debug", ".NETFramework,Version=v4.8.AssemblyAttributes.cs");

            var project = ObjectModelHelpers.CreateInMemoryProject($@"
<Project>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.Common.props"" />
  <PropertyGroup>
    <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include=""a.cs""/>
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />

  <Target Name=""CopyFilesToOutputDirectory""/>

  <Target Name=""CoreCompile"">
    <Error Text=""Expected '@(Compile)' == '{expectedCompileItems}'""
           Condition=""'@(Compile)' != '{expectedCompileItems}'""/>
  </Target>
</Project>
");
            var logger = new MockLogger(_output);
            bool result = project.Build(logger);
            Assert.True(result, "Output:" + Environment.NewLine + logger.FullLog);
        }

        /// <summary>
        /// Test for https://github.com/dotnet/msbuild/issues/8153
        /// </summary>
        [Fact]
        public void IsWellKnownAttributeValuePreserved()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            ObjectModelHelpers.CreateFileInTempProjectDirectory("Myapp.proj", @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name =`Repro`>
                    <CreateItem Include=`*.txt` AdditionalMetadata=`MyProperty=Identity`>
                      <Output TaskParameter=`Include` ItemName=`TestItem`/>
                    </CreateItem>
                    <Error Text=`@(TestItem)` Condition=""'%(MyProperty)' != 'Identity' ""/>
                  </Target>
                </Project>
                ");

            ObjectModelHelpers.CreateFileInTempProjectDirectory("Foo.txt", "foo");
            MockLogger logger = new MockLogger(_output);
            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("Myapp.proj", logger);
        }

        /// <summary>
        /// Test for https://github.com/dotnet/msbuild/issues/13478.
        /// </summary>
        [Fact]
        public void CopyUpToDateMarkerTracksImplementationAssemblyWrites()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            CreateCopyMarkerTestProject(produceReferenceAssembly: true);

            MockLogger logger = new MockLogger(_output);

            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("MarkerTest.csproj", logger);

            string markerPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "obj", "Debug", "MarkerTest.csproj.Up2Date");
            File.Exists(markerPath).ShouldBeTrue($"Expected marker to exist at {markerPath}");
            DateTime markerAfterFirstBuild = File.GetLastWriteTimeUtc(markerPath);

            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("MarkerTest.csproj", logger);

            DateTime markerAfterNoOpBuild = File.GetLastWriteTimeUtc(markerPath);
            markerAfterNoOpBuild.ShouldBe(markerAfterFirstBuild);

            string intermediateRefAssemblyPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "obj", "Debug", "refint", "MarkerTest.dll");
            File.Exists(intermediateRefAssemblyPath).ShouldBeTrue($"Expected intermediate reference assembly to exist at {intermediateRefAssemblyPath}");

            Thread.Sleep(TimeSpan.FromSeconds(2));
            File.WriteAllText(intermediateRefAssemblyPath, "Reference output changed");

            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("MarkerTest.csproj", logger);

            DateTime markerAfterReferenceAssemblyOnlyChange = File.GetLastWriteTimeUtc(markerPath);
            markerAfterReferenceAssemblyOnlyChange.ShouldBe(markerAfterNoOpBuild);

            Thread.Sleep(TimeSpan.FromSeconds(2));
            File.WriteAllText(Path.Combine(ObjectModelHelpers.TempProjectDir, "input.txt"), "changed");

            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("MarkerTest.csproj", logger);

            DateTime markerAfterImplementationChange = File.GetLastWriteTimeUtc(markerPath);
            (markerAfterImplementationChange > markerAfterReferenceAssemblyOnlyChange).ShouldBeTrue(
                $"Expected marker to advance from {markerAfterReferenceAssemblyOnlyChange:O}, but it was {markerAfterImplementationChange:O}.");
        }

        /// <summary>
        /// Test for https://github.com/dotnet/msbuild/issues/13478.
        /// </summary>
        [Fact]
        public void CopyUpToDateMarkerIsNotCreatedForMainAssemblyWritesWhenReferenceAssembliesAreNotProduced()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();
            CreateCopyMarkerTestProject(produceReferenceAssembly: false);

            MockLogger logger = new MockLogger(_output);

            ObjectModelHelpers.BuildTempProjectFileExpectSuccess("MarkerTest.csproj", logger);

            string markerPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "obj", "Debug", "MarkerTest.csproj.Up2Date");
            File.Exists(markerPath).ShouldBeFalse($"Did not expect marker to exist at {markerPath}");
        }

        private static void CreateCopyMarkerTestProject(bool produceReferenceAssembly)
        {
            ObjectModelHelpers.CreateFileInTempProjectDirectory("input.txt", "initial");
            ObjectModelHelpers.CreateFileInTempProjectDirectory("MarkerTest.csproj", $@"
<Project DefaultTargets=""Build"" xmlns=""msbuildnamespace"" ToolsVersion=""msbuilddefaulttoolsversion"">
  <Import Project=""$(MSBuildToolsPath)\Microsoft.Common.props"" />

  <PropertyGroup>
    <AssemblyName>MarkerTest</AssemblyName>
    <OutputType>Library</OutputType>
    <TargetFrameworkVersion>{MSBuildConstants.StandardTestTargetFrameworkVersion}</TargetFrameworkVersion>
    <ProduceReferenceAssembly>{produceReferenceAssembly.ToString().ToLowerInvariant()}</ProduceReferenceAssembly>
  </PropertyGroup>

  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />

  <Target Name=""GetReferenceAssemblyPaths"" />
  <Target Name=""ResolveAssemblyReferences"" />

  <Target Name=""CoreCompile""
          Inputs=""input.txt""
          Outputs=""@(IntermediateAssembly)"">
    <MakeDir Directories=""@(IntermediateAssembly->'%(RootDir)%(Directory)');@(IntermediateRefAssembly->'%(RootDir)%(Directory)')"" />
    <WriteLinesToFile File=""@(IntermediateAssembly)""
                      Lines=""Implementation output""
                      Overwrite=""true"" />
    <WriteLinesToFile File=""@(IntermediateRefAssembly)""
                      Lines=""Reference output""
                      Overwrite=""true""
                      Condition=""'@(IntermediateRefAssembly)' != '' and !Exists('@(IntermediateRefAssembly)')"" />
  </Target>
</Project>");
        }
    }
}
