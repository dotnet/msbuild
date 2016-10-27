// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Test white space preservation.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;

using Xunit;
using System.Linq;
using Microsoft.Build.Evaluation;


namespace Microsoft.Build.UnitTests.OM.Construction
{
    public class WhitespacePreservation_Tests
    {
        [Theory]
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>
</Project>",

@"<Project xmlns=`msbuildnamespace`>
  <ItemGroup />
</Project>")]

        [InlineData(
@"<Project xmlns=`msbuildnamespace`>


</Project>",

@"<Project xmlns=`msbuildnamespace`>
  <ItemGroup />
</Project>")]
        public void AddEmptyParent(string projectContents, string updatedProject)
        {
            AssertWhiteSpacePreservation(projectContents, updatedProject, (pe, p) =>
            {
                var itemGroup = pe.AddItemGroup();

                Assert.True(p.IsDirty);
            });
        }

        [Theory]
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>

</Project>",

@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>

  <ItemGroup>
    <i2 Include=`b` />
  </ItemGroup>

</Project>")]
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>


  <ItemGroup>

    <i Include=`a` />

  </ItemGroup>


</Project>",

@"<Project xmlns=`msbuildnamespace`>


  <ItemGroup>

    <i Include=`a` />

  </ItemGroup>


  <ItemGroup>
    <i2 Include=`b` />
  </ItemGroup>


</Project>")]
        public void AddParentAndChild(string projectContents, string updatedProject)
        {
            AssertWhiteSpacePreservation(projectContents, updatedProject, (pe, p) =>
            {
                var itemGroup = pe.AddItemGroup();

                itemGroup.AddItem("i2", "b");

                Assert.True(p.IsDirty);
            });
        }

        [Theory]

        // no new lines are added
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>
  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>
  <ItemGroup>
  </ItemGroup>
</Project>",

@"<Project xmlns=`msbuildnamespace`>
  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>
  <ItemGroup>
    <i2 Include=`b` />
  </ItemGroup>
</Project>")]

        // new lines between parents are preserved
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>


  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>


  <ItemGroup>
  </ItemGroup>

</Project>",

@"<Project xmlns=`msbuildnamespace`>


  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>


  <ItemGroup>
    <i2 Include=`b` />
  </ItemGroup>

</Project>")]

        // parent has no indentation but has leading whitespace. Indentation is the whitespace after the last new line in the parent's entire leading whitespace
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>                           <ItemGroup>
  </ItemGroup>

</Project>",

@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>                           <ItemGroup>
  <i2 Include=`b` />
</ItemGroup>

</Project>")]

        // parent has no leading whitespace
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup><ItemGroup>
  </ItemGroup>

</Project>",

@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup><ItemGroup>
  <i2 Include=`b` />
</ItemGroup>

</Project>")]

        // empty parent has no whitespace in it; append new line and the parent's indentation
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>

  <ItemGroup></ItemGroup>

</Project>",

@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>

  <ItemGroup>
    <i2 Include=`b` />
  </ItemGroup>

</Project>")]

        // the initial whitespace in the empty parent gets replaced with newline + parent_indentation
        [InlineData(
@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>

  <ItemGroup>    
    
        
            
                
                    </ItemGroup>

</Project>",

@"<Project xmlns=`msbuildnamespace`>

  <ItemGroup>
    <i Include=`a` />
  </ItemGroup>

  <ItemGroup>
    <i2 Include=`b` />
  </ItemGroup>

</Project>")]
        public void AddFirstChildInExistingParent(string projectContents, string updatedProject)
        {
            AssertWhiteSpacePreservation(projectContents, updatedProject,
                (pe, p) => { pe.ItemGroups.ElementAt(1).AddItem("i2", "b"); });
        }

        private void AssertWhiteSpacePreservation(string projectContents, string updatedProject,
            Action<ProjectRootElement, Project> act)
        {
            var projectElement =
                ProjectRootElement.Create(
                    XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(projectContents))),
                    ProjectCollection.GlobalProjectCollection,
                    true);

            var project = new Project(projectElement);

            act(projectElement, project);

            var writer = new StringWriter();
            project.Save(writer);

            var expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                           ObjectModelHelpers.CleanupFileContents(updatedProject);
            var actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        private void VerifyAssertLineByLine(string expected, string actual)
        {
            Helpers.VerifyAssertLineByLine(expected, actual, false);
        }
    }
}
