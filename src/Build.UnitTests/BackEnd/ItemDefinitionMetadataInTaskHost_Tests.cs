// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Item definition metadata referencing built-in metadata, such as <c>%(Filename)</c>, is stored unexpanded
    /// and substituted when the metadata is read. These tests assert that a task observes the same value whether
    /// it runs in-proc or in a task host.
    /// Regression tests for https://github.com/dotnet/msbuild/issues/14763.
    /// </summary>
    public sealed class ItemDefinitionMetadataInTaskHost_Tests
    {
        private static string AssemblyLocation { get; } =
            typeof(ItemDefinitionMetadataInTaskHost_Tests).Assembly.Location
            ?? Path.Combine(AppContext.BaseDirectory, "Microsoft.Build.Engine.UnitTests.dll");

        private readonly ITestOutputHelper _output;

        public ItemDefinitionMetadataInTaskHost_Tests(ITestOutputHelper output) => _output = output;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MetadataReferencingBuiltInMetadataIsExpandedForTheTask(bool useTaskHost)
        {
            Observe("%(Filename)", useTaskHost).ShouldBe("hello");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MetadataReferencingBuiltInMetadataFollowsReassignedItemSpec(bool useTaskHost)
        {
            Observe("%(Filename)", useTaskHost, newItemSpec: @"other\renamed.txt").ShouldBe("renamed");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MetadataOverriddenOnTheItemWinsOverTheItemDefinition(bool useTaskHost)
        {
            Observe("%(Filename)", useTaskHost, itemOverride: "explicit").ShouldBe("explicit");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EscapedMetadataReferenceIsNotExpanded(bool useTaskHost)
        {
            Observe("%25(Filename)", useTaskHost).ShouldBe("%(Filename)");
        }

        /// <summary>
        /// Runs a task against an item whose definition carries <paramref name="definitionValue"/> and returns the
        /// value the task itself observed, having asserted the task ran where the test intended.
        /// </summary>
        private string Observe(string definitionValue, bool useTaskHost, string newItemSpec = null, string itemOverride = null)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string project = $"""
                <Project>
                  <UsingTask TaskName="MetadataObservationTask" AssemblyFile="{AssemblyLocation}"{(useTaskHost ? @" TaskFactory=""TaskHostFactory""" : string.Empty)} />
                  <ItemDefinitionGroup>
                    <Thing>
                      <NameMeta>{definitionValue}</NameMeta>
                    </Thing>
                  </ItemDefinitionGroup>
                  <ItemGroup>
                    <Thing Include="folder\hello.txt">
                      {(itemOverride is null ? string.Empty : $"<NameMeta>{itemOverride}</NameMeta>")}
                    </Thing>
                  </ItemGroup>
                  <Target Name="Observe">
                    <MetadataObservationTask Items="@(Thing)" MetadataName="NameMeta" NewItemSpec="{newItemSpec}">
                      <Output PropertyName="ObservedValue" TaskParameter="ObservedValue" />
                      <Output PropertyName="TaskProcessId" TaskParameter="TaskProcessId" />
                    </MetadataObservationTask>
                  </Target>
                </Project>
                """;

            ProjectInstance projectInstance = new(env.CreateFile("test.proj", project).Path);

            BuildResult result = BuildManager.DefaultBuildManager.Build(
                new BuildParameters { EnableNodeReuse = false },
                new BuildRequestData(projectInstance, targetsToBuild: ["Observe"]));

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            int taskProcessId = int.Parse(projectInstance.GetPropertyValue("TaskProcessId"));
            bool ranOutOfProc = taskProcessId != Process.GetCurrentProcess().Id;
            ranOutOfProc.ShouldBe(useTaskHost, $"the task was expected to run {(useTaskHost ? "in a task host" : "in-proc")}");

            return projectInstance.GetPropertyValue("ObservedValue");
        }
    }
}
