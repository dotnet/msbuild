// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using InternalUtilities = Microsoft.Build.Internal.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Tests some manipulations of Project and ProjectCollection that require dealing with internal data.
    /// </summary>
    public class Project_Internal_Tests
    {
        /// <summary>
        /// Set default tools version; subsequent projects should use it
        /// </summary>
        [Fact]
        public void SetDefaultToolsVersion()
        {
            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore the default
                // ToolsVersion in the ProjectCollection.  However, this test explicitly depends on modifying
                // that, so we need to turn the new defaulting behavior off in order to verify that this still works.
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                ProjectCollection collection = new ProjectCollection();
                collection.AddToolset(new Toolset("x", @"c:\y", collection, null));

                collection.DefaultToolsVersion = "x";

                Assert.Equal("x", collection.DefaultToolsVersion);

                string content = @"
                    <Project>
                        <Target Name='t'/>
                    </Project>
                ";

                Project project = new Project(XmlReader.Create(new StringReader(content)), null, null, collection);

                Assert.Equal("x", project.ToolsVersion);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        /// <summary>
        /// If the ToolsVersion in the project file is bogus, we'll default to the current ToolsVersion and successfully
        /// load it.  Make sure we can RE-load it, too, and successfully pick up the correct copy of the loaded project.
        ///
        /// ... Make sure we can do this even if we're not using the "always default everything to current anyway" codepath.
        /// </summary>
        [Fact]
        public void ReloadProjectWithInvalidToolsVersionInFile()
        {
            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore the default
                // ToolsVersion in the ProjectCollection.  However, this test explicitly depends on modifying
                // that, so we need to turn the new defaulting behavior off in order to verify that this still works.
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                string content = @"
                    <Project ToolsVersion='bogus'>
                        <Target Name='t'/>
                    </Project>
                ";

                Project project = new Project(XmlReader.Create(new StringReader(content)));
                project.FullPath = "c:\\123.proj";

                Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject("c:\\123.proj", null, null);

                Assert.True(Object.ReferenceEquals(project, project2));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        /// <summary>
        /// Project.ToolsVersion should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void ProjectToolsVersion20Present()
        {
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                // "Requires 2.0 to be installed"
                return;
            }

            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore what
                // is written in the project file.  However, this test explicitly depends on effectively
                // modifying the "project file" (through the construction model OM), so we need to turn
                // that behavior off in order to verify that it still works.
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                Project project = new Project();
                project.Xml.ToolsVersion = "2.0";
                project.ReevaluateIfNecessary();

                Assert.Equal("2.0", project.ToolsVersion);

                project.Xml.ToolsVersion = "4.0";

                Assert.Equal("2.0", project.ToolsVersion);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        [Fact]
        public void UsingExplicitToolsVersionShouldBeFalseWhenNoToolsetIsReferencedInProject()
        {
            var project = ObjectModelHelpers.CreateInMemoryProject("<Project></Project>");

            project.TestOnlyGetPrivateData.UsingDifferentToolsVersionFromProjectFile.ShouldBeFalse();
        }

        /// <summary>
        /// $(MSBuildToolsVersion) should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void MSBuildToolsVersionProperty()
        {
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                // "Requires 2.0 to be installed"
                return;
            }

            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore what
                // is written in the project file.  However, this test explicitly depends on effectively
                // modifying the "project file" (through the construction model OM), so we need to turn
                // that behavior off in order to verify that it still works.
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                Project project = new Project();
                project.Xml.ToolsVersion = "2.0";
                project.ReevaluateIfNecessary();

                Assert.Equal("2.0", project.GetPropertyValue("msbuildtoolsversion"));

                project.Xml.ToolsVersion = ObjectModelHelpers.MSBuildDefaultToolsVersion;
                Assert.Equal("2.0", project.GetPropertyValue("msbuildtoolsversion"));

                project.ReevaluateIfNecessary();

                Assert.Equal(
                    ObjectModelHelpers.MSBuildDefaultToolsVersion,
                    project.GetPropertyValue("msbuildtoolsversion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        [Fact]
        public void ProjectEvaluationShouldRespectConditionsIfProjectLoadSettingsSaysSo()
        {
            var projectContents = @"
<Project>
   <ItemDefinitionGroup Condition=`1 == 2`>
     <I>
       <m>v</m>
     </I>
   </ItemDefinitionGroup>

   <PropertyGroup Condition=`1 == 2`>
     <P1>v</P1>
   </PropertyGroup>

   <PropertyGroup>
     <P2 Condition=`1 == 2`>v</P2>
   </PropertyGroup>

   <ItemGroup Condition=`1 == 2`>
     <I1 Include=`i`/>
   </ItemGroup>

   <ItemGroup>
     <I2 Condition=`1 == 2` Include=`i`/>
   </ItemGroup>
</Project>".Cleanup();

            using (var env = TestEnvironment.Create())
            {
                var projectCollection = env.CreateProjectCollection().Collection;

                var project = new Project(XmlReader.Create(new StringReader(projectContents)), new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, projectCollection, ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition);

                var data = project.TestOnlyGetPrivateData;

                project.GetProperty("P1").ShouldBeNull();
                project.GetProperty("P2").ShouldBeNull();
                project.Items.ShouldBeEmpty();
                project.ItemDefinitions.ShouldBeEmpty();

                data.ConditionedProperties.ShouldBeEmpty();
                data.ItemsIgnoringCondition.ShouldBeEmpty();
                data.AllEvaluatedItemDefinitionMetadata.ShouldBeEmpty();
                data.AllEvaluatedItems.ShouldBeEmpty();

                project.ConditionedProperties.ShouldBeEmpty();
                project.AllEvaluatedItemDefinitionMetadata.ShouldBeEmpty();
                project.AllEvaluatedItems.ShouldBeEmpty();

                Should.Throw<InvalidOperationException>(() =>
                {
                    var c = project.ItemsIgnoringCondition;
                });
            }
        }

        /// <summary>
        /// Verifies that when calling <see cref="Project.FromFile(string, ProjectOptions)" /> with <see cref="ProjectOptions.Interactive" /> <see langword="true" />, the built-in &quot;MSBuildInteractive&quot; property is set to <see langword="true" />, otherwise the property is <see cref="string.Empty" />.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ProjectFromFileInteractive(bool interactive)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                ProjectCollection projectCollection = testEnvironment.CreateProjectCollection().Collection;

                ProjectRootElement projectRootElement = ProjectRootElement.Create(projectCollection);

                projectRootElement.Save(testEnvironment.CreateFile().Path);

                Project project = Project.FromFile(
                    projectRootElement.FullPath,
                    new ProjectOptions
                    {
                        Interactive = interactive,
                        ProjectCollection = projectCollection,
                    });

                project.GetPropertyValue(ReservedPropertyNames.interactive).ShouldBe(interactive ? bool.TrueString : string.Empty, StringCompareShould.IgnoreCase);
            }
        }

        /// <summary>
        /// Verifies that when calling <see cref="Project.FromProjectRootElement(ProjectRootElement, ProjectOptions)" /> with <see cref="ProjectOptions.Interactive" /> <see langword="true" />, the built-in &quot;MSBuildInteractive&quot; property is set to <see langword="true" />, otherwise the property is <see cref="string.Empty" />.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ProjectFromProjectRootElementInteractive(bool interactive)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                ProjectCollection projectCollection = testEnvironment.CreateProjectCollection().Collection;

                ProjectRootElement projectRootElement = ProjectRootElement.Create(projectCollection);

                projectRootElement.Save(testEnvironment.CreateFile().Path);

                Project project = Project.FromProjectRootElement(
                    projectRootElement,
                    new ProjectOptions
                    {
                        Interactive = interactive,
                        ProjectCollection = projectCollection,
                    });

                project.GetPropertyValue(ReservedPropertyNames.interactive).ShouldBe(interactive ? bool.TrueString : string.Empty, StringCompareShould.IgnoreCase);
            }
        }

        /// <summary>
        /// Verifies that when calling <see cref="Project.FromXmlReader(XmlReader, ProjectOptions)" /> with <see cref="ProjectOptions.Interactive" /> <see langword="true" />, the built-in &quot;MSBuildInteractive&quot; property is set to <see langword="true" />, otherwise the property is <see cref="string.Empty" />.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ProjectFromXmlReaderInteractive(bool interactive)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                ProjectCollection projectCollection = testEnvironment.CreateProjectCollection().Collection;

                ProjectRootElement projectRootElement = ProjectRootElement.Create(projectCollection);

                projectRootElement.Save(testEnvironment.CreateFile().Path);

                using (XmlReader xmlReader = XmlReader.Create(projectRootElement.FullPath))
                {
                    Project project = Project.FromXmlReader(
                        xmlReader,
                        new ProjectOptions
                        {
                            Interactive = interactive,
                            ProjectCollection = projectCollection,
                        });

                    project.GetPropertyValue(ReservedPropertyNames.interactive).ShouldBe(interactive ? bool.TrueString : string.Empty, StringCompareShould.IgnoreCase);
                }
            }
        }
    }
}
