// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class LiveLinkProbeEvaluation_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void BothModelsPreserveGlobObserversMetadataAndRemoval()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string include = Path.Combine("**", "*.cs");
        string exclude = Path.Combine("**", "obj", "**");
        string child = Path.Combine("src", "child.cs");
        string content = $$"""
            <Project>
              <ItemDefinitionGroup>
                <Compile><Origin>definition</Origin></Compile>
              </ItemDefinitionGroup>
              <ItemGroup>
                <Compile Include="{{include}}" Exclude="{{exclude}}">
                  <Version>initial</Version>
                </Compile>
                <Before Include="@(Compile)">
                  <Observed>%(FullPath)</Observed>
                </Before>
                <Compile Update="@(Compile)">
                  <Version>updated</Version>
                </Compile>
                <After Include="@(Compile)" />
                <Compile Remove="{{child}}" />
              </ItemGroup>
            </Project>
            """;
        TransientTestProjectWithFiles fixture = environment.CreateTestProjectWithFiles(content,
            ["root.cs", child, Path.Combine("obj", "excluded.cs")]);
        using ProjectCollection collection = new();
        collection.RegisterLogger(new MockLogger(_output));
        Project project = new(fixture.ProjectFile, null, null, collection);
        Verify(project.GetItems("Before").Select(Read).ToArray(),
            project.GetItems("After").Select(Read).ToArray(), project.GetItems("Compile").Select(Read).ToArray());
        ProjectInstance instance = new(fixture.ProjectFile, null, null, collection);
        Verify(instance.GetItems("Before").Select(Read).ToArray(),
            instance.GetItems("After").Select(Read).ToArray(), instance.GetItems("Compile").Select(Read).ToArray());

        void Verify(Item[] before, Item[] after, Item[] final)
        {
            before.Select(item => item.Include).ShouldBe(["root.cs", child]);
            after.Select(item => item.Include).ShouldBe(["root.cs", child]);
            final.Select(item => item.Include).ShouldBe(["root.cs"]);
            foreach (Item item in before)
            {
                item.Version.ShouldBe("initial");
                item.Origin.ShouldBe("definition");
                item.Observed.ShouldBe(item.FullPath);
            }
            foreach (Item item in after.Concat(final))
            {
                item.Version.ShouldBe("updated");
                item.Origin.ShouldBe("definition");
                item.FullPath.ShouldBe(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fixture.ProjectFile)!, item.Include)));
            }
        }
    }

    private static Item Read(ProjectItem item) => new(item.EvaluatedInclude, item.GetMetadataValue("Version"),
        item.GetMetadataValue("Origin"), item.GetMetadataValue("FullPath"), item.GetMetadataValue("Observed"));
    private static Item Read(ProjectItemInstance item) => new(item.EvaluatedInclude, item.GetMetadataValue("Version"),
        item.GetMetadataValue("Origin"), item.GetMetadataValue("FullPath"), item.GetMetadataValue("Observed"));
    private sealed record Item(string Include, string Version, string Origin, string FullPath, string Observed);
}
