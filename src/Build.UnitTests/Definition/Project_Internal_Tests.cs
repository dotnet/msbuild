// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Shouldly;
using InternalUtilities = Microsoft.Build.Internal.Utilities;
using Xunit;

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
        public void SetDefaultToolsVersionShouldBeIgnored()
        {
            ProjectCollection collection = new ProjectCollection();
            collection.AddToolset(new Toolset("x", @"c:\y", collection, null));
            collection.DefaultToolsVersion = "x";

            collection.DefaultToolsVersion.ShouldBe("x");

            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'/>
                    </Project>
                ";

            Project project = new Project(XmlReader.Create(new StringReader(content)), null, null, collection);

            // Ensure that the "Current" ToolsVersion is always used
            project.ToolsVersion.ShouldBe(MSBuildConstants.CurrentToolsVersion);
        }

        [Fact]
        public void UsingExplicitToolsVersionShouldBeFalseWhenNoToolsetIsReferencedInProject()
        {
            var project = ObjectModelHelpers.CreateInMemoryProject("<Project></Project>");

            project.TestOnlyGetPrivateData.UsingDifferentToolsVersionFromProjectFile.ShouldBeFalse();
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
    }
}
