// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Construction;

using Xunit;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;


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
                pe.AddItemGroup();

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

    <i2 Include=`b` />

  </ItemGroup>
</Project>")]
        // AddItem ends up calling InsertAfterChild
        public void AddChildWithExistingSiblingsViaAddItem(string projectContents, string updatedProject)
        {
            AssertWhiteSpacePreservation(projectContents, updatedProject,
                (pe, p) => { pe.ItemGroups.First().AddItem("i2", "b"); });
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
    <i2 Include=`b` />
    <i Include=`a` />
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
    <i2 Include=`b` />
    <i Include=`a` />

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

    <i2 Include=`b` />

    <i Include=`a` />
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

    <i2 Include=`b` />

    <i Include=`a` />

  </ItemGroup>
</Project>")]
        public void AddChildWithExistingSiblingsViaInsertBeforeChild(string projectContents, string updatedProject)
        {
            AssertWhiteSpacePreservation(projectContents, updatedProject,
                (pe, p) =>
                {
                    var itemGroup = pe.ItemGroups.First();
                    var existingItemElement = itemGroup.FirstChild;
                    var newItemElement = itemGroup.ContainingProject.CreateItemElement("i2", "b");

                    itemGroup.InsertBeforeChild(newItemElement, existingItemElement);
                });
        }

        [Fact]
        public void VerifySaveProjectContainsCorrectLineEndings()
        {
            var project = @"<Project xmlns=`msbuildnamespace`>
  <ItemGroup> <!-- comment here -->

    <i Include=`a` /> <!--
multi-line comment here

-->

  </ItemGroup>
</Project>
";
            string expected = @"<Project xmlns=`msbuildnamespace`>
  <ItemGroup> <!-- comment here -->

    <i2 Include=`b` />

    <i Include=`a` /> <!--
multi-line comment here

-->

  </ItemGroup>
</Project>
";
            // Use existing test to add a sibling and verify the output is as expected (including comments)
            AddChildWithExistingSiblingsViaInsertBeforeChild(project, expected);
        }

        private void AssertWhiteSpacePreservation(
            string projectContents,
            string updatedProject,
            Action<ProjectRootElement, Project> act)
        {
            // Each OS uses its own line endings. Using WSL on Windows leads to LF on Windows which messes up the tests. This happens due to git LF <-> CRLF conversions.
            if (NativeMethodsShared.IsWindows)
            {
                projectContents = Regex.Replace(projectContents, @"(?<!\r)\n", "\r\n", RegexOptions.Multiline);
                updatedProject = Regex.Replace(updatedProject, @"(?<!\r)\n", "\r\n", RegexOptions.Multiline);
            }
            else
            {
                projectContents = Regex.Replace(projectContents, @"\r\n", "\n", RegexOptions.Multiline);
                updatedProject = Regex.Replace(updatedProject, @"\r\n", "\n", RegexOptions.Multiline);
            }

            // Note: This test will write the project file to disk rather than using in-memory streams.
            // Using streams can cause issues with CRLF characters being replaced by LF going in to
            // ProjectRootElement. Saving to disk mimics the real-world behavior so we can specifically
            // test issues with CRLF characters being normalized. Related issue: #1340
            var file = FileUtilities.GetTemporaryFile();
            var expected = ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual;

            try
            {
                // Write the projectConents to disk and load it
                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents(projectContents));
                var projectElement = ProjectRootElement.Open(file, ProjectCollection.GlobalProjectCollection, true);
                var project = new Project(projectElement);

                act(projectElement, project);

                // Write the project to a UTF8 string writer to compare against
                var writer = new EncodingStringWriter();
                project.Save(writer);
                actual = writer.ToString();
            }
            finally
            {
                FileUtilities.DeleteNoThrow(file);
            }

            VerifyAssertLineByLine(expected, actual);

            VerifyLineEndings(actual);
        }

        private void VerifyAssertLineByLine(string expected, string actual)
        {
            Helpers.VerifyAssertLineByLine(expected, actual, false);
        }

        /// <summary>
        /// Ensure that all line-endings in the save result are correct for the current OS
        /// </summary>
        /// <param name="projectResults">Project file contents after save.</param>
        private void VerifyLineEndings(string projectResults)
        {
            if (Environment.NewLine.Length == 2)
            {
                // Windows, ensure that \n doesn't exist by itself
                var crlfCount = Regex.Matches(projectResults, @"\r\n", RegexOptions.Multiline).Count;
                var nlCount = Regex.Matches(projectResults, @"\n").Count;

                // Compare number of \r\n to number of \n, they should be equal.
                Assert.Equal(crlfCount, nlCount);
            }
            else
            {
                // Ensure we did not add \r\n
                Assert.Empty(Regex.Matches(projectResults, @"\r\n", RegexOptions.Multiline));
            }
        }
    }
}
