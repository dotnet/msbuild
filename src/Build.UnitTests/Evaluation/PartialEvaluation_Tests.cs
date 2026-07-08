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

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Tests for the opt-in partial (stop-after-pass) evaluation model exposed via
    /// <see cref="ProjectOptions.EvaluationStage"/>.
    /// </summary>
    public class PartialEvaluation_Tests
    {
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

        private static ProjectOptions OptionsFor(ProjectEvaluationStage stage) => new ProjectOptions
        {
            EvaluationStage = stage,
            ProjectCollection = new ProjectCollection(),
        };

        [Fact]
        public void DefaultProjectOptionsStageIsFull()
        {
            new ProjectOptions().EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
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
            Should.Throw<InvalidOperationException>(() => instance.GetItems("Compile"));
            Should.Throw<InvalidOperationException>(() => instance.Targets);
            Should.Throw<InvalidOperationException>(() => instance.DefaultTargets);
        }

        [Fact]
        public void PropertiesStage_ExposesPropertiesButNotItemsOrTargets_Project()
        {
            Project project = Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));

            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);
            project.GetPropertyValue("Derived").ShouldBe("Debug-x");

            Should.Throw<InvalidOperationException>(() => project.Items);
            Should.Throw<InvalidOperationException>(() => project.GetItems("Compile"));
            Should.Throw<InvalidOperationException>(() => project.AllEvaluatedItems);
            Should.Throw<InvalidOperationException>(() => project.Targets);
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
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), new ProjectOptions { ProjectCollection = new ProjectCollection() });

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            instance.GetItems("Compile").Count.ShouldBe(2);
            instance.Targets.ShouldContainKey("Build");
        }

        [Fact]
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

        [Fact]
        public void CreateProjectInstanceFromPartialProjectUpgradesToFull()
        {
            Project project = Project.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));
            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);

            ProjectInstance instance = project.CreateProjectInstance();

            instance.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            instance.Targets.ShouldContainKey("Build");
            project.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
        }

        [Fact]
        public void PartialProjectInstanceCannotBeBuilt()
        {
            ProjectInstance instance = ProjectInstance.FromProjectRootElement(CreateRootElement(), OptionsFor(ProjectEvaluationStage.Properties));

            Should.Throw<InvalidOperationException>(() => new BuildRequestData(instance, new[] { "Build" }));
        }

        [Fact]
        public void CacheDoesNotServePartialProjectForFullLoad()
        {
            using ProjectCollection collection = new ProjectCollection();
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".proj");
            File.WriteAllText(path, ProjectXml);

            try
            {
                Project partial = Project.FromFile(path, new ProjectOptions
                {
                    EvaluationStage = ProjectEvaluationStage.Properties,
                    ProjectCollection = collection,
                });
                partial.EvaluationStage.ShouldBe(ProjectEvaluationStage.Properties);

                // A subsequent full LoadProject on the same key must return a fully-evaluated project
                // (the cached partial one is upgraded in place), never partial state.
                Project full = collection.LoadProject(path);
                full.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
                full.Targets.ShouldContainKey("Build");

                // The partial and full references point at the same upgraded cached project.
                ReferenceEquals(partial, full).ShouldBeTrue();
                partial.EvaluationStage.ShouldBe(ProjectEvaluationStage.Full);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
