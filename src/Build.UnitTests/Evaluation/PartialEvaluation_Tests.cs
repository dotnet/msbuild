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

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Tests for the opt-in partial (stop-after-pass) evaluation model exposed via
    /// <see cref="ProjectOptions.EvaluationStage"/>.
    /// </summary>
    [TestClass]
    public class PartialEvaluation_Tests : IDisposable
    {
        private readonly TestContext _output;
        private readonly TestEnvironment _env;
        private readonly ProjectCollection _collection = new ProjectCollection();

        public PartialEvaluation_Tests(TestContext output)
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

        [MSBuildTestMethod]
        public void DefaultProjectOptionsStageIsFull()
        {
            new ProjectOptions().EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
        }

        [MSBuildTestMethod]
        [DataRow(0)]
        [DataRow(5)]
        [DataRow(-1)]
        [DataRow(int.MaxValue - 1)]
        public void EvaluationStage_RejectsUndefinedValues(int value)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new ProjectOptions { EvaluationStage = (ProjectEvaluationStage)value });
        }

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        public void PropertiesStage_ExposesPropertiesButNotItemsOrTargets_Project()
        {
            Project project = Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));

            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);
            project.GetPropertyValue("Derived").ShouldBe("Debug-x");

            Should.Throw<InvalidOperationException>(() => project.Items);
            Should.Throw<InvalidOperationException>(() => project.ItemDefinitions);
            Should.Throw<InvalidOperationException>(() => project.GetItems("Compile"));
            Should.Throw<InvalidOperationException>(() => project.AllEvaluatedItems);
            Should.Throw<InvalidOperationException>(() => project.Targets);
        }

        [MSBuildTestMethod]
        public void PropertyValuesEqualFullEvaluation()
        {
            ProjectInstance partial = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));
            ProjectInstance full = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Full));

            partial.GetPropertyValue("Derived").ShouldBe(full.GetPropertyValue("Derived"));

            // Item references in property values expand to empty even in a full evaluation because
            // properties are evaluated before items, so both stages agree.
            partial.GetPropertyValue("FromItem").ShouldBe(full.GetPropertyValue("FromItem"));
        }

        [MSBuildTestMethod]
        public void ItemDefinitionsStage_ExposesItemDefinitionsButNotItemsOrTargets()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.ItemDefinitions));

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.ItemDefinitions);
            Should.NotThrow(() => instance.ItemDefinitions);

            Should.Throw<InvalidOperationException>(() => instance.Items);
            Should.Throw<InvalidOperationException>(() => instance.Targets);
        }

        [MSBuildTestMethod]
        public void ItemsStage_ExposesItemsButNotTargets()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Items));

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Items);
            instance.GetItems("Compile").Count.ShouldBe(2);

            Should.Throw<InvalidOperationException>(() => instance.Targets);
        }

        [MSBuildTestMethod]
        public void FullStage_IsDefault_AndExposesEverything()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), new ProjectOptions { ProjectCollection = _collection });

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            instance.GetItems("Compile").Count.ShouldBe(2);
            instance.Targets.ShouldContainKey("Build");
        }

        [MSBuildTestMethod]
        public void ReevaluateUpgradesPartialProjectToFull()
        {
            Project project = Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));

            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);
            Should.Throw<InvalidOperationException>(() => project.Targets);

            project.ReevaluateIfNecessary();

            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            project.Targets.ShouldContainKey("Build");
            project.GetItems("Compile").Count.ShouldBe(2);
        }

        [MSBuildTestMethod]
        public void CreateProjectInstanceFromPartialProjectUpgradesToFull()
        {
            Project project = Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));
            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);

            ProjectInstance instance = project.CreateProjectInstance();

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            instance.Targets.ShouldContainKey("Build");
            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
        }

        [MSBuildTestMethod]
        public void PartialProjectInstanceCannotBeBuilt()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));

            Should.Throw<InvalidOperationException>(() => new BuildRequestData(instance, new[] { "Build" }));
        }

        [MSBuildTestMethod]
        public void CacheDoesNotServePartialProjectForFullLoad()
        {
            TransientTestFile file = _env.CreateFile("test.proj", ProjectXml);

            Project partial = Project.FromFile(file.Path, new ProjectOptions
            {
                EvaluationStage = ProjectEvaluationStage.Properties,
                ProjectCollection = _collection,
            });
            partial.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);

            // A subsequent full LoadProject on the same key must return a fully-evaluated project
            // (the cached partial one is upgraded in place), never partial state.
            Project full = _collection.LoadProject(file.Path);
            full.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            full.Targets.ShouldContainKey("Build");

            // The partial and full references point at the same upgraded cached project.
            ReferenceEquals(partial, full).ShouldBeTrue();
            partial.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
        }
    }
}
