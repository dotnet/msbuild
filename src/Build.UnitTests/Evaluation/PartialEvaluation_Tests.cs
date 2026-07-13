// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Tests for the opt-in partial (stop-after-pass) evaluation model exposed via
    /// <see cref="ProjectOptions.EvaluationStage"/>.
    /// </summary>
    public class PartialEvaluation_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly ProjectCollection _collection = new ProjectCollection();

        public PartialEvaluation_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(_output);
        }

        public void Dispose()
        {
            _collection.Dispose();
            _env.Dispose();
        }

        private const string ProjectXml = """
            <Project>
              <PropertyGroup>
                <Config>Debug</Config>
                <Derived>$(Config)-x</Derived>
                <FromItem>@(Compile)</FromItem>
              </PropertyGroup>
              <ItemDefinitionGroup>
                <Compile>
                  <Kind>source</Kind>
                </Compile>
              </ItemDefinitionGroup>
              <ItemGroup>
                <Compile Include="a.cs" />
                <Compile Include="b.cs" />
              </ItemGroup>
              <UsingTask TaskName="Dummy" AssemblyName="Some.Assembly" />
              <Target Name="Build" />
              <Target Name="Other" />
            </Project>
            """;

        private static ProjectRootElement CreateRootElement()
        {
            using XmlReader reader = XmlReader.Create(new StringReader(ProjectXml));
            return ProjectRootElement.Create(reader);
        }

        private ProjectOptions OptionsFor(ProjectEvaluationStage stage) => new ProjectOptions
        {
            EvaluationStage = stage,
            ProjectCollection = _collection,
        };

        [Fact]
        public void DefaultProjectOptionsStageIsFull()
        {
            new ProjectOptions().EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(-1)]
        [InlineData(int.MaxValue - 1)]
        public void EvaluationStage_RejectsUndefinedValues(int value)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new ProjectOptions { EvaluationStage = (ProjectEvaluationStage)value });
        }

        [Fact]
        public void PropertiesStage_ExposesPropertiesButNotItemsOrTargets_ProjectInstance()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);
            instance.GetPropertyValue("Derived").ShouldBe("Debug-x");

            // InitialTargets are computed during pass 1 and remain available.
            Should.NotThrow(() => instance.InitialTargets);

            Should.Throw<InvalidOperationException>(() => instance.Items);
            Should.Throw<InvalidOperationException>(() => instance.ItemDefinitions);
            Should.Throw<InvalidOperationException>(() => instance.GetItems("Compile"));
            Should.Throw<InvalidOperationException>(() => instance.Targets);
            Should.Throw<InvalidOperationException>(() => instance.DefaultTargets);
        }

        [Fact]
        public void ProjectFactories_RejectPartialEvaluationStage()
        {
            // Project only supports full evaluation. A partial stage passed through ProjectOptions
            // must be rejected up front rather than silently ignored.
            Should.Throw<ArgumentException>(() =>
                Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties)));

            Should.Throw<ArgumentException>(() =>
                Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Items)));
        }

        [Fact]
        public void ProjectFactories_RejectNullOptions()
        {
            // The partial-stage guard is the first thing the factories do, so it must not dereference
            // a null ProjectOptions; callers get the canonical ArgumentNullException instead.
            Should.Throw<ArgumentNullException>(() =>
                Project.FromProjectRootElement(CreateRootElement(), null));
        }

        [Fact]
        public void ProjectFactories_AllowFullEvaluationStage()
        {
            Project project = Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Full));

            project.GetPropertyValue("Derived").ShouldBe("Debug-x");
            project.GetItems("Compile").Count.ShouldBe(2);
            project.Targets.ShouldContainKey("Build");
        }

        [Fact]
        public void PropertyValuesEqualFullEvaluation()
        {
            ProjectInstance partial = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));
            ProjectInstance full = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Full));

            partial.GetPropertyValue("Derived").ShouldBe(full.GetPropertyValue("Derived"));

            // Item references in property values expand to empty even in a full evaluation because
            // properties are evaluated before items, so both stages agree.
            partial.GetPropertyValue("FromItem").ShouldBe(full.GetPropertyValue("FromItem"));
        }

        [Fact]
        public void ItemDefinitionsStage_ExposesItemDefinitionsButNotItemsOrTargets()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.ItemDefinitions));

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.ItemDefinitions);
            Should.NotThrow(() => instance.ItemDefinitions);

            Should.Throw<InvalidOperationException>(() => instance.Items);
            Should.Throw<InvalidOperationException>(() => instance.Targets);
        }

        [Fact]
        public void ItemsStage_ExposesItemsButNotTargets()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Items));

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Items);
            instance.GetItems("Compile").Count.ShouldBe(2);

            Should.Throw<InvalidOperationException>(() => instance.Targets);
        }

        [Fact]
        public void FullStage_IsDefault_AndExposesEverything()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), new ProjectOptions { ProjectCollection = _collection });

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            instance.GetItems("Compile").Count.ShouldBe(2);
            instance.Targets.ShouldContainKey("Build");
        }

        [Fact]
        public void PartialProjectInstanceCannotBeBuilt()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));

            Should.Throw<InvalidOperationException>(() => new BuildRequestData(instance, new[] { "Build" }));
        }
    }
}
