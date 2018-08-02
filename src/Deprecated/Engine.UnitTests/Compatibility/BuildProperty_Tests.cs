// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Xml;
using System.Collections.Generic;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Tests for BuildProperty
    /// </summary>
    [TestFixture]
    public class BuildProperty_Tests
    {
        #region Constructor Tests
        /// <summary>
        /// Tests BuildProperty Contructor with a simple string for Name and Value
        /// </summary>
        [Test]
        public void ConstructWithSimpleStringNameAndValue()
        {
            BuildProperty property = new BuildProperty("n", "v");

            Assertion.AssertEquals("n", property.Name);
            Assertion.AssertEquals("v", property.Value);
        }

        /// <summary>
        /// Tests BuildProperty Contructor with a String.Empty for Name and  a simple string for Value
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ConstructWithEmptyStringNameAndSpringStringValue()
        {
            BuildProperty property = new BuildProperty(String.Empty, "v");
        }

        /// <summary>
        /// Tests BuildProperty Contructor with a simple string for Name and String.Empty for Value
        /// </summary>
        [Test]
        public void ConstructWithSimpleStringNameAndEmptyStringValue()
        {
            BuildProperty property = new BuildProperty("n", String.Empty);

            Assertion.AssertEquals("n", property.Name);
            Assertion.AssertEquals(String.Empty, property.Value);
        }

        /// <summary>
        /// Tests BuildProperty Contructor with null for Name and Value
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructWithNullNameValue()
        {
            BuildProperty property = new BuildProperty(null, null);
        }

        /// <summary>
        /// Tests BuildProperty Contructor with non valid xml name
        /// </summary>
        /// <remarks>cliffh and jaysh brought this up during dev10 development</remarks>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetNameToNonValidXml()
        {
            BuildProperty property = new BuildProperty("1invalid", "v");
        }

        /// <summary>
        /// Tests BuildProperty Contructor with null for Name and a simple string for Value
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructWithNullNameAndSimpleStringValue()
        {
            BuildProperty property = new BuildProperty(null, "v");
        }

        /// <summary>
        /// Tests BuildProperty Contructor with a simple string for Name and null for Value
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructWithSimpleStringNameAndNullValue()
        {
            BuildProperty property = new BuildProperty("n", null);
        }
        #endregion

        #region Value Tests
        /// <summary>
        /// Tests setting the BuildProperty Value to another string
        /// </summary>
        [Test]
        public void SetValueToAnotherString()
        {
            BuildProperty property = new BuildProperty("n", "v");
            Assertion.AssertEquals("v", property.Value);

            // Set Value to another string
            property.Value = "new";

            Assertion.AssertEquals("new", property.Value);
        }

        /// <summary>
        /// Tests setting the BuildProperty Value to String.Empty
        /// </summary>
        [Test]
        public void SetValueToEmptyString()
        {
            BuildProperty property = new BuildProperty("n", "v");
            Assertion.AssertEquals("v", property.Value);

            // Set Value to an empty string
            property.Value = String.Empty;

            Assertion.AssertEquals(string.Empty, property.Value);
        }

        /// <summary>
        /// Tests setting the BuildProperty Value to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetValueToNull()
        {
            BuildProperty property = new BuildProperty("n", "v");
            Assertion.AssertEquals("v", property.Value);

            property.Value = null;
        }
        #endregion

        #region Clone Tests
        /// <summary>
        /// Tests BuildProperty Clone for a Deep Clone
        /// </summary>
        [Test]
        public void CloneDeep()
        {
            BuildProperty property = new BuildProperty("n", "v");
            BuildProperty clone = property.Clone(true);

            Assertion.AssertEquals(property.Name, clone.Name);
            Assertion.AssertEquals(property.Value, clone.Value);

            property.Value = "new";
            clone.Value = "other";

            Assertion.AssertEquals("other", clone.Value);
            Assertion.AssertEquals("new", property.Value);
            Assertion.AssertEquals("n", property.Name);
            Assertion.AssertEquals("n", clone.Name);
        }

        /// <summary>
        /// Tests BuildProperty Clone for a Shallow Clone
        /// </summary>
        [Test]
        public void CloneShallowOfImportedProject()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "outdir");

            BuildProperty clone = property.Clone(false);

            Assertion.AssertEquals(property.Name, clone.Name);
            Assertion.AssertEquals(property.Value, clone.Value);
        }

        /// <summary>
        /// Tests BuildProperty Clone for a Shallow Clone by attempting to change the value of a Build Property item
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CloneShallowAttemptToChangeValueOfImportedProject()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "outdir");

            BuildProperty clone = property.Clone(false);

            Assertion.AssertEquals(property.Name, clone.Name);
            Assertion.AssertEquals(property.Value, clone.Value);

            property.Value = "new";
        }

        /// <summary>
        /// Tests BuildProperty Clone for a Shallow Clone of an in-memory property
        /// <remarks>
        ///     Suggestion for v10 OM - return the message in the InvalidOperationException on why this is invalid
        ///     Why this fails "it's just an in-memory property.  We can't do a shallow clone for this type of property,
        ///     because there's no XML element for the clone to share."  (this is only available from the code)
        /// </remarks>
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CloneShallow_Invalid()
        {
            BuildProperty property = new BuildProperty("n", "v");
            BuildProperty clone = property.Clone(false);
        }
        #endregion

        #region ToString Tests
        /// <summary>
        /// Tests BuildProperty ToString with a simple string Value
        /// </summary>
        [Test]
        public void BuildPropertyToStringWithSimpleStringValue()
        {
            BuildProperty property = new BuildProperty("n", "v");
            Assertion.AssertEquals("v", property.ToString());
        }

        /// <summary>
        /// Tests BuildProperty ToString with an String.Empty Value
        /// </summary>
        [Test]
        public void BuildPropertyToStringWithEmptyStringValue()
        {
            BuildProperty property = new BuildProperty("n", String.Empty);
            Assertion.AssertEquals(String.Empty, property.ToString());
        }

        /// <summary>
        /// Tests BuildProperty ToString after modifying the Value
        /// </summary>
        [Test]
        public void BuildPropertyToStringAfterModifyingValue()
        {
            BuildProperty property = new BuildProperty("n", "v");
            property.Value = "new";
            Assertion.AssertEquals("new", property.ToString());
        }
        #endregion

        #region IsImported Tests
        /// <summary>
        /// Tests BuildProperty IsImported simple case
        /// </summary>
        [Test]
        public void IsImportedExpectedFalse()
        {
            BuildProperty property = new BuildProperty("n", "v");
            Assertion.AssertEquals(false, property.IsImported);
        }

        /// <summary>
        /// Tests BuildProperty IsImported when build property is only imported
        /// </summary>
        [Test]
        public void IsImportedExpectedTrueWhenOnlyFromImport()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "outdir");

            Assertion.AssertEquals(true, property.IsImported);
        }

        /// <summary>
        /// Tests BuildProperty IsImported when build property is not imported
        /// </summary>
        [Test]
        public void IsImportedExpectedFalseComesDirectlyFromProject()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "f");

            Assertion.AssertEquals(false, property.IsImported);
        }

        /// <summary>
        /// Tests BuildProperty IsImported when build property comes directly from the project and is also imported
        /// </summary>
        [Test]
        public void IsImportedExpectedTrueComesDirectlyFromProjectAndImport()
        {
            Project p = GetProjectWithProperties(new string[] { "<MaxTargetPath>234</MaxTargetPath>" });

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "maxtargetpath");

            Assertion.AssertEquals(true, property.IsImported);
        }

        /// <summary>
        /// Tests BuildProperty IsImported when build property is imported and comes directly from the project
        /// </summary>
        [Test]
        public void IsImportedExpectedFalseComesFromImportAndDirectlyFromProject()
        {
            Project p = GetProjectWithProperties(new string[] { "<MaxTargetPath>234</MaxTargetPath>" });

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "maxtargetpath", false);

            Assertion.AssertEquals(false, property.IsImported);
        }
        #endregion

        #region Condition Tests
        /// <summary>
        /// Tests BuildProperty Condition when a condition exists
        /// </summary>
        [Test]
        public void ConditionGetWhenConditionExists()
        {
            string s = "<n3 Condition='true'>v3</n3>";
            Project p = GetProjectWithProperties(new string[] { s });

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "n3");

            Assertion.AssertEquals("true", property.Condition);
        }

        /// <summary>
        /// Tests BuildProperty Condition when no condition exists
        /// </summary>
        [Test]
        public void ConditionGetWhenNoConditionExists()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "f");

            Assertion.AssertEquals(String.Empty, property.Condition);
        }

        /// <summary>
        /// Tests BuildProperty Condition, setting a condition when no previous condition exists
        /// </summary>
        [Test]
        public void ConditionSetWhenNoConditionExists()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "f");
            property.Condition = "true";

            Assertion.AssertEquals("true", property.Condition);
        }

        /// <summary>
        /// Tests BuildProperty Condition, setting a condition when no previous condition exists
        /// </summary>
        [Test]
        public void ConditionSetWithNoPreviousCondition()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "f");
            property.Condition = "true";

            Assertion.AssertEquals("true", property.Condition);

            p.Build();

            BuildProperty prop = GetSpecificBuildPropertyFromProject(p, null, "f");
            Console.WriteLine("'{0}'", prop.Condition);
        }

        /// <summary>
        /// Tests BuildProperty Condition, setting a condition when a previous condition exists
        /// </summary>
        [Test]
        public void ConditionChangingWhenConditionExists()
        {
            Project p = GetProjectWithProperties();

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "b");
            property.Condition = "false";
            Assertion.AssertEquals("false", property.Condition);
        }

        /// <summary>
        /// Tests BuildProperty Condition when the Condition includes an Item Group
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ConditionItemGroupInAPropertyCondition()
        {
            string projectContent = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <a Condition=`@(x)=='x1'`>@(x)</a>
                    </PropertyGroup>
                    <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                    </Target>
                </Project>
            ";
            Project p = ObjectModelHelpers.CreateInMemoryProject(projectContent);

            p.Build(new string[] { "t" }, null);
        }

        /// <summary>
        /// Tests BuildProperty Before/After a Build
        /// </summary>
        [Test]
        public void BuildPropertyCreatedDuringBuild()
        {
            string projectContents = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`Build`>
                        <PropertyGroup>
                            <n>v</n>
                        </PropertyGroup>
                    </Target>
                </Project>
                ";

            Project p = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            Assertion.AssertEquals(0, p.PropertyGroups.Count);
            Assertion.AssertEquals(null, p.GetEvaluatedProperty("n"));

            p.Build();

            Assertion.AssertEquals("v", p.GetEvaluatedProperty("n"));
            Assertion.AssertEquals(0, p.PropertyGroups.Count);
        }
        #endregion

        #region FinalValue Tests
        /// <summary>
        /// Tests BuildProperty FinalValue when value is a simple string
        /// </summary>
        [Test]
        public void FinalValueWithSimpleStringValue()
        {
            BuildProperty property = new BuildProperty("n", "v");
            Assertion.AssertEquals("v", property.FinalValue);
        }

        /// <summary>
        /// Tests BuildProperty FinalValue when value is an empty string
        /// </summary>
        [Test]
        public void FinalValueWithEmptyStringValue()
        {
            BuildProperty property = new BuildProperty("n", String.Empty);
            Assertion.AssertEquals(String.Empty, property.FinalValue);
        }

        /// <summary>
        /// Tests BuildProperty FinalValue when value comes from a project
        /// </summary>
        [Test]
        public void FinalValueWithInProject()
        {
            Project p = GetProjectWithProperties(new string[] { "<j>v1</j>" });

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "j");

            Assertion.AssertEquals("v1", property.FinalValue);
        }

        /// <summary>
        /// Tests BuildProperty FinalValue when value contains escape characters
        /// </summary>
        [Test]
        public void FinalValueWhereValueContainsEscapeCharacters()
        {
            Project p = GetProjectWithProperties(new string[] { "<j>The&amp;Fat&amp;Cat&amp;RanUpThe&gt;Road&lt;ToMeetTheThinDog</j>" });

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "j");

            Assertion.AssertEquals("The&Fat&Cat&RanUpThe>Road<ToMeetTheThinDog", property.FinalValue);
        }
        #endregion

        #region ExplicitOperatorString Tests
        /// <summary>
        /// Tests static explicit operator string
        /// </summary>
        [Test]
        public void StaticExplicitOperatorString()
        {
            BuildProperty property = new BuildProperty("n", "v");
            Assertion.AssertEquals("v", (string)property);
        }

        /// <summary>
        /// Tests static explicit operator string when value is an empty string
        /// </summary>
        [Test]
        public void StaticExplicitOperatorStringWithEmptyStringValue()
        {
            BuildProperty property = new BuildProperty("n", String.Empty);
            Assertion.AssertEquals(String.Empty, (string)property);
        }

        /// <summary>
        /// Tests static explicit operator string when value comes from a project
        /// </summary>
        [Test]
        public void StaticExplicitOperatorStringWithInProject()
        {
            Project p = GetProjectWithProperties(new string[] { "<j>v1</j>" });

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "j");

            Assertion.AssertEquals("v1", (string)property);
        }

        /// <summary>
        /// Tests static explicit operator string when value contains escape characters
        /// </summary>
        [Test]
        public void StaticExplicitOperatorStringWhereValueContainsEscapeCharacters()
        {
            Project p = GetProjectWithProperties(new string[] { "<Jar>The&amp;Fat&amp;Cat&amp;RanUpThe&gt;Road&lt;ToMeetTheThinDog</Jar>" });

            BuildProperty property = GetSpecificBuildPropertyFromProject(p, null, "jar");

            Assertion.AssertEquals("The&Fat&Cat&RanUpThe>Road<ToMeetTheThinDog", (string)property);
        }
        #endregion

        #region BuildProperty Helpers
        /// <summary>
        /// Gets a specified Build Projecty from a given project, assumes specified Property expected First
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="property">BuildProperty</param>
        /// <param name="buildPropertyName">Specific Build Property that you want</param>
        /// <returns>The specified Build Property</returns>
        private static BuildProperty GetSpecificBuildPropertyFromProject(Project p, BuildProperty property, string buildPropertyName)
        {
            return GetSpecificBuildPropertyFromProject(p, property, buildPropertyName, true);
        }

        /// <summary>
        /// Gets a specified Build Property from a given project
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="property">BuildProperty</param>
        /// <param name="buildPropertyName">Specific Build Property that you want</param>
        /// <param name="expectedPropertyFirst">True if expected Property comes first, False expected property comes second</param>
        /// <returns>The specified Build Property</returns>
        private static BuildProperty GetSpecificBuildPropertyFromProject(Project p, BuildProperty property, string buildPropertyName, bool expectedPropertyFirst)
        {
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                foreach (BuildProperty prop in group)
                {
                    if (String.Equals(prop.Name, buildPropertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        property = prop;
                        if (expectedPropertyFirst)
                        {
                            break;
                        }
                        else
                        {
                            return property;
                        }
                    }
                }
            }

            return property;
        }

        /// <summary>
        /// Creates a Project with default XML Content
        /// </summary>
        /// <returns>Project p with default XML Content</returns>
        private static Project GetProjectWithProperties()
        {
            return GetProjectWithProperties(new string[] { });
        }

        /// <summary>
        /// Creates a Project with XML Content.  Basic XML content is defined, you can pass in additional Build Properties
        /// </summary>
        /// <returns>Project p with default XML Content plus additional specified Build Property Items</returns>
        private static Project GetProjectWithProperties(string[] propertyGroupBuildItemAtAddIntoContent)
        {
            string projectContent;

            if (propertyGroupBuildItemAtAddIntoContent.Length == 0)
            {
                projectContent = ObjectModelHelpers.CleanupFileContents(@"
                          <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace' >
                            <PropertyGroup>
                                <f>fvalue</f>
                                <b Condition='true'>bvalue</b>
                            </PropertyGroup>
                            <Import Project='$(MSBuildBinPath)\Microsoft.Common.targets' />
                          </Project>");
            }
            else
            {
                string contentToInsert = string.Empty;
                foreach (string s in propertyGroupBuildItemAtAddIntoContent)
                {
                    contentToInsert += s;
                }

                projectContent = String.Format
                    (ObjectModelHelpers.CleanupFileContents(
                        @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace' >
                            <PropertyGroup>
                                <f>fvalue</f>
                                <b Condition='true'>bvalue</b>
                                {0}
                            </PropertyGroup>
                            <Import Project='$(MSBuildBinPath)\Microsoft.Common.targets' />
                          </Project>"),
                    contentToInsert
                    );
            }

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContent);
            return p;
        }
        #endregion
    }
}
