// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Tests for BuildPropertyGroup
    /// </summary>
    [TestFixture]
    public class BuildPropertyGroup_Tests
    {
        #region Helper Fields
        /// <summary>
        /// Basic project content start
        /// </summary>
        private string basicProjectContentsBefore = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <p1>true</p1>
                        <p2>4</p2>
                    </PropertyGroup>

                    <PropertyGroup>
                        <p3>v</p3>
                    </PropertyGroup>
                </Project>
                ";

        /// <summary>
        /// Basic project content with only one property group
        /// </summary>
        private string basicProjectContentsWithOnePropertyGroup = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <PropertyGroup>
                                    <n>v</n>
                                </PropertyGroup>
                            </Project>
                            ";
        #endregion

        #region Constructor Tests
        /// <summary>
        /// Tests BuildPropertyGroup Contructor with no parameters
        /// </summary>
        [Test]
        public void ConstructWithNothing()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();

            Assertion.AssertEquals(0, group.Count);
            Assertion.AssertEquals(false, group.IsImported);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Contructor with a simple project
        /// </summary>
        [Test]
        public void ConstructWithSimpleProject()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);
            BuildPropertyGroup group = new BuildPropertyGroup(p);

            Assertion.AssertEquals(0, group.Count);
            Assertion.AssertEquals(false, group.IsImported);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Contructor with a project that contains specific property groups and items
        /// </summary>
        [Test]
        public void ConstructWithProject()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);

            List<string> properties = GetListOfBuildProperties(p);

            Assertion.AssertEquals("p1", properties[0]);
            Assertion.AssertEquals("p2", properties[1]);
            Assertion.AssertEquals("p3", properties[2]);
        }
        #endregion

        #region Count Tests
        /// <summary>
        /// Tests BuildPropertyGroup Count with only one property set
        /// </summary>
        [Test]
        public void CountOne()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", "v");

            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals("v", group["n"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Count with no properties set
        /// </summary>
        [Test]
        public void CountZero()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();

            Assertion.AssertEquals(0, group.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Count after clearing it.
        /// </summary>
        [Test]
        public void CountAfterClear()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", "v");
            group.Clear();

            Assertion.AssertEquals(0, group.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Count after removing 1 of the properties
        /// </summary>
        [Test]
        public void CountAfterRemovingSomeProperties()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");

            group.RemoveProperty("n1");

            Assertion.AssertEquals(1, group.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Count after removing 1 of the properties
        /// </summary>
        [Test]
        public void CountAfterRemovingAllProperties()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");
            group.SetProperty("n3", "v3");

            group.RemoveProperty("n1");
            group.RemoveProperty("n2");
            group.RemoveProperty("n3");

            Assertion.AssertEquals(0, group.Count);
        }
        #endregion

        #region Clone Tests
        /// <summary>
        /// Tests BuildPropertyGroup Clone Deep
        /// </summary>
        [Test]
        public void CloneDeep()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");

            BuildPropertyGroup clone = group.Clone(true);

            Assertion.AssertEquals(2, clone.Count);
            Assertion.AssertEquals(clone["n1"].Value, group["n1"].Value);
            Assertion.AssertEquals(clone["n2"].Value, group["n2"].Value);

            group.SetProperty("n1", "new");

            Assertion.AssertEquals("new", group["n1"].Value);
            Assertion.AssertEquals("v1", clone["n1"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Clone Deep with Clear
        /// </summary>
        [Test]
        public void CloneDeepClear()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");

            BuildPropertyGroup clone = group.Clone(true);
            clone.Clear();

            Assertion.AssertEquals(0, clone.Count);
            Assertion.AssertEquals(2, group.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Clone Shallow
        /// </summary>
        [Test]
        public void CloneShallow()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");

            BuildPropertyGroup clone = group.Clone(false);
            group["n1"].Value = "new";

            Assertion.AssertEquals(2, clone.Count);
            Assertion.AssertEquals(2, group.Count);
            Assertion.AssertEquals("new", group["n1"].Value);
            Assertion.AssertEquals("new", clone["n1"].Value);
            Assertion.AssertEquals(clone["n2"].Value, group["n2"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Clone Shallow with Set Property
        /// </summary>
        [Test]
        public void CloneShallowSetProperty()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");

            BuildPropertyGroup clone = group.Clone(false);

            group.SetProperty("n1", "new");

            Assertion.AssertEquals(2, clone.Count);
            Assertion.AssertEquals(2, group.Count);
            Assertion.AssertEquals("new", group["n1"].Value);
            Assertion.AssertEquals("v1", clone["n1"].Value);
            Assertion.AssertEquals(clone["n2"].Value, group["n2"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup Clone Shallow when you attempt a shallow clone of an XMLProject
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CloneShallowWithXMLProject()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);

            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                BuildPropertyGroup clone = group.Clone(false);
            }
        }
        #endregion

        #region SetProperty Tests
        /// <summary>
        /// Tests BuildPropertyGroup SetProperty Value to an empty string
        /// </summary>
        [Test]
        public void SetPropertyEmptyValue()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", String.Empty);

            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals(String.Empty, group["n"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty Value to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetPropertyNullValue()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", null);
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty Name to an empty string
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetPropertyEmptyName()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty(String.Empty, "v");
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty Name to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetPropertyNullName()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty(null, "v");
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty setting Name and Value to a simple value
        /// </summary>
        [Test]
        public void SetPropertySimpleNameValue()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", "v");

            Assertion.AssertEquals("v", group["n"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty with Treat Property Value as Literal set to true/false
        /// </summary>
        [Test]
        public void SetPropertyTreatPropertyValueAsLiteral()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", @"%*?@$();\", true);
            group.SetProperty("n2", @"%*?@$();\", false);

            Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", group["n1"].Value);
            Assertion.AssertEquals(@"%*?@$();\", group["n2"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty with Treat Property Value as Literal set to false
        /// </summary>
        [Test]
        public void SetPropertyTreatPropertyValueAsLiteralFalse()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", "v", false);

            Assertion.AssertEquals("v", group["n"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty with a project p
        /// </summary>
        [Test]
        public void SetPropertyWithProject()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);

            List<string> properties = GetListOfBuildProperties(p);
            properties[0] = "v";

            Assertion.AssertEquals("v", properties[0]);
            Assertion.AssertEquals("p2", properties[1]);
            Assertion.AssertEquals("p3", properties[2]);
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty Value with special characters
        /// </summary>
        [Test]
        public void SetPropertyValueWithSpecialCharacters()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "%24");
            group.SetProperty("n2", "%40");
            group.SetProperty("n3", "%3b");
            group.SetProperty("n4", "%5c");
            group.SetProperty("n5", "%25");

            Assertion.AssertEquals("$", group["n1"].FinalValue);
            Assertion.AssertEquals("@", group["n2"].FinalValue);
            Assertion.AssertEquals(";", group["n3"].FinalValue);
            Assertion.AssertEquals(@"\", group["n4"].FinalValue);
            Assertion.AssertEquals("%", group["n5"].FinalValue);
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetProperty Name with special characters
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetPropertyNameWithSpecialCharacters()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("%24", "v");
        }
        #endregion

        #region RemoveProperty Tests
        /// <summary>
        /// Tests BuildPropertyGroup RemoveProperty by removing 1 of many by name
        /// </summary>
        [Test]
        public void RemovePropertyByNameOneOfSeveral()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");
            group.SetProperty("n3", "v3");

            group.RemoveProperty("n1");

            Assertion.AssertEquals(2, group.Count);
            Assertion.AssertNull(group["n1"]);
            Assertion.AssertEquals("v2", group["n2"].Value);
            Assertion.AssertEquals("v3", group["n3"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup RemoveProperty by removing all of many by name
        /// </summary>
        [Test]
        public void RemovePropertyByNameAllOfSeveral()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");
            group.SetProperty("n3", "v3");

            group.RemoveProperty("n1");
            group.RemoveProperty("n2");
            group.RemoveProperty("n3");

            Assertion.AssertEquals(0, group.Count);
            Assertion.AssertNull(group["n1"]);
            Assertion.AssertNull(group["n2"]);
            Assertion.AssertNull(group["n3"]);
        }

        /// <summary>
        /// Tests BuildPropertyGroup RemoveProperty by attempting to remove a property that doesn't actually exist
        /// </summary>
        [Test]
        public void RemovePropertyByNameOfANonExistingProperty()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", "v");

            group.RemoveProperty("not");

            Assertion.AssertEquals(1, group.Count);
        }

        /// <summary>
        ///  Tests BuildPropertyGroup RemoveProperty by attempting to remove a property name that is null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RemovePropertyByNameWhenNameIsNull()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            string name = null;

            group.RemoveProperty(name);
        }

        /// <summary>
        /// Tests BuildPropertyGroup RemoveProperty by attempting to remove a property name that is an empty string
        /// </summary>
        [Test]
        public void RemovePropertyByNameThatIsAnEmptyString()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n", "v");

            group.RemoveProperty(String.Empty);

            Assertion.AssertEquals(1, group.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroup RemoveProperty by removing 1 of several properties by BuildProperty
        /// </summary>
        [Test]
        public void RemovePropertyByBuildPropertyOneOfSeveral()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");
            group.SetProperty("n3", "v3");

            BuildProperty property = GetSpecificBuildPropertyOutOfBuildPropertyGroup(group, "n2");

            group.RemoveProperty(property);

            Assertion.AssertEquals(2, group.Count);
            Assertion.AssertEquals("v1", group["n1"].Value);
            Assertion.AssertNull(group["n2"]);
            Assertion.AssertEquals("v3", group["n3"].Value);
        }

        /// <summary>
        /// Tests BuildPropertyGroup RemoveProperty by all of several properties by BuildProperty
        /// </summary>
        [Test]
        public void RemovePropertyByBuildPropertyAllOfSeveral()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");
            group.SetProperty("n3", "v3");

            BuildProperty[] property = new BuildProperty[] 
                {
                    GetSpecificBuildPropertyOutOfBuildPropertyGroup(group, "n1"),
                    GetSpecificBuildPropertyOutOfBuildPropertyGroup(group, "n2"),
                    GetSpecificBuildPropertyOutOfBuildPropertyGroup(group, "n3")
                };

            group.RemoveProperty(property[0]);
            group.RemoveProperty(property[1]);
            group.RemoveProperty(property[2]);

            Assertion.AssertEquals(0, group.Count);
            Assertion.AssertNull(group["n1"]);
            Assertion.AssertNull(group["n2"]);
            Assertion.AssertNull(group["n3"]);
        }

        /// <summary>
        /// Tests BuildPropertyGroup RemoveProperty of a null property
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RemovePropertyByBuildPropertyWithNullProperty()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            BuildProperty property = null;

            group.RemoveProperty(property);
        }
        #endregion

        #region AddNewProperty Tests
        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with setting Name to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddNewPropertyNameToNull()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroup(p, null, "v");
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with setting Name to an empty string
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void AddNewPropertyNameToEmptyString()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroup(p, string.Empty, "v");
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with setting Value to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddNewPropertyValueToNull()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroup(p, "n", null);
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with setting Value to an empty string
        /// </summary>
        [Test]
        public void AddNewPropertyValueToEmptyString()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroup(p, "n", String.Empty);

            string projectContentsAfter = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <p1>true</p1>
                        <p2>4</p2>
                        <n></n>
                    </PropertyGroup>

                    <PropertyGroup>
                        <p3>v</p3>
                        <n></n>
                    </PropertyGroup>
                </Project>
                ";

            Build.UnitTests.ObjectModelHelpers.CompareProjectContents(p, projectContentsAfter);
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with simple Name and Value
        /// </summary>
        [Test]
        public void AddNewPropertySimpleNameValue()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroup(p, "n", "v");

            string projectContentsAfter = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <p1>true</p1>
                        <p2>4</p2>
                        <n>v</n>
                    </PropertyGroup>

                    <PropertyGroup>
                        <p3>v</p3>
                        <n>v</n>
                    </PropertyGroup>
                </Project>
                ";

            Build.UnitTests.ObjectModelHelpers.CompareProjectContents(p, projectContentsAfter);
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty several sets
        /// </summary>
        [Test]
        public void AddNewPropertySeveralNameValuePairs()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroup(p, "n1", "v1");
            AddNewPropertyToEachPropertyGroup(p, "n2", "v2");
            AddNewPropertyToEachPropertyGroup(p, "n3", "v3");
            AddNewPropertyToEachPropertyGroup(p, "n4", "v4");

            string projectContentsAfter = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <p1>true</p1>
                        <p2>4</p2>
                        <n1>v1</n1>
                        <n2>v2</n2>
                        <n3>v3</n3>
                        <n4>v4</n4>
                    </PropertyGroup>

                    <PropertyGroup>
                        <p3>v</p3>
                        <n1>v1</n1>
                        <n2>v2</n2>
                        <n3>v3</n3>
                        <n4>v4</n4>
                    </PropertyGroup>
                </Project>
                ";

            Build.UnitTests.ObjectModelHelpers.CompareProjectContents(p, projectContentsAfter);
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with simple Name and Value as well as setting Treat Property Value as Literal to true
        /// </summary>
        [Test]
        public void AddNewPropertySimpleNameValueWithTreatPropertyValueAsLiteralTrue()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroupWithPropertyValueAsLiteral(p, "n", @"%*?@$();\", true);

            string projectContentsAfter = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <p1>true</p1>
                        <p2>4</p2>
                        <n>%25%2a%3f%40%24%28%29%3b\</n>
                    </PropertyGroup>

                    <PropertyGroup>
                        <p3>v</p3>
                        <n>%25%2a%3f%40%24%28%29%3b\</n>
                    </PropertyGroup>
                </Project>
                ";

            Build.UnitTests.ObjectModelHelpers.CompareProjectContents(p, projectContentsAfter);
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with simple Name and Value as well as setting Treat Property Value as Literal to false
        /// </summary>
        [Test]
        public void AddNewPropertySimpleNameValueWithTreatPropertyValueAsLiteralFalse()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            AddNewPropertyToEachPropertyGroupWithPropertyValueAsLiteral(p, "n", @"%*?@$();\", false);

            string projectContentsAfter = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <p1>true</p1>
                        <p2>4</p2>
                        <n>%*?@$();\</n>
                    </PropertyGroup>

                    <PropertyGroup>
                        <p3>v</p3>
                        <n>%*?@$();\</n>
                    </PropertyGroup>
                </Project>
                ";

            Build.UnitTests.ObjectModelHelpers.CompareProjectContents(p, projectContentsAfter);
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with special characters for the Name
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void AddNewPropertyWithSpecialCharactersInName()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);
            AddNewPropertyToEachPropertyGroup(p, "%25", "v1");
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with special characters for the Value
        /// </summary>
        [Test]
        public void AddNewPropertyWithSpecialCharactersInValue()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);
            AddNewPropertyToEachPropertyGroup(p, "n1", "%24%40%3b%5c%25");

            Dictionary<string, string> properties = GetDictionaryOfPropertiesInProject(p);

            Assertion.AssertEquals("%24%40%3b%5c%25", properties["n1"].ToString());
        }

        /// <summary>
        /// Tests BuildPropertyGroup AddNewProperty with an in memory build property group
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddNewPropertyToInMemoryGroup()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");

            group.AddNewProperty("n3", "v3");
        }
        #endregion

        #region Condition tests
        /// <summary>
        /// Tests BuildPropertyGroup Condition Get
        /// </summary>
        [Test]
        public void ConditionGetWhenSetItXML()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup Condition="" '$(Foo)' == 'Bar' "">
                        <n1>v1</n1>
                        <n2>v2</n2>
                        <n3 Condition='true'>v3</n3>
                    </PropertyGroup>
                    <PropertyGroup Condition="" '$(A)' == 'B' "">
                        <n4>v4</n4>
                    </PropertyGroup>
                    <PropertyGroup Condition=""'$(C)' == 'D'"">
                    </PropertyGroup>
                    <PropertyGroup Condition="""">
                        <n5>v5</n5>
                    </PropertyGroup>
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);

            List<string> conditions = GetListOfBuildPropertyGroupConditions(p);

            Assertion.AssertEquals(" '$(Foo)' == 'Bar' ", conditions[0].ToString());
            Assertion.AssertEquals(" '$(A)' == 'B' ", conditions[1].ToString());
            Assertion.AssertEquals("'$(C)' == 'D'", conditions[2].ToString());
            Assertion.AssertEquals(String.Empty, conditions[3].ToString());
        }

        /// <summary>
        /// Tests BuildPropertyGroup Condition Set for a simple set
        /// </summary>
        [Test]
        public void ConditionSetSimple()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);
            SetPropertyGroupConditionOnEachPropertyGroup(p, "'$(C)' == 'D'");

            List<string> conditions = GetListOfBuildPropertyGroupConditions(p);

            Assertion.AssertEquals("'$(C)' == 'D'", conditions[0].ToString());
        }

        /// <summary>
        /// Tests BuildPropertyGroup Condition Set to multiple PropertyGroups
        /// </summary>
        [Test]
        public void ConditionSetToMultipleGroups()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsBefore);
            SetPropertyGroupConditionOnEachPropertyGroup(p, "'$(C)' == 'D'");

            List<string> conditions = GetListOfBuildPropertyGroupConditions(p);

            Assertion.AssertEquals("'$(C)' == 'D'", conditions[0].ToString());
            Assertion.AssertEquals("'$(C)' == 'D'", conditions[1].ToString());
        }

        /// <summary>
        /// Tests BuildPropertyGroup Condition Set after setting the condition (setting two times in a row)
        /// </summary>
        [Test]
        public void ConditionSetAfterSetting()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);
            SetPropertyGroupConditionOnEachPropertyGroup(p, "'$(C)' == 'D'");
            SetPropertyGroupConditionOnEachPropertyGroup(p, " '$(Foo)' == 'Bar' ");

            List<string> conditions = GetListOfBuildPropertyGroupConditions(p);

            Assertion.AssertEquals(" '$(Foo)' == 'Bar' ", conditions[0].ToString());
        }

        /// <summary>
        /// Tests BuildPropertyGroup Condition Set with an  escape character like lessthan
        /// </summary>
        [Test]
        public void ConditionSetWithEscapeCharacter()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);
            SetPropertyGroupConditionOnEachPropertyGroup(p, "'&lt;' == 'D'");

            List<string> conditions = GetListOfBuildPropertyGroupConditions(p);

            Assertion.AssertEquals("'&lt;' == 'D'", conditions[0].ToString());
        }

        /// <summary>
        /// Tests BuildPropertyGroup Condition Set with Special Characters (; $ @ \ $ %)
        /// </summary>
        [Test]
        public void ConditionSetWithSpecialCharacters()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsWithOnePropertyGroup);
            SetPropertyGroupConditionOnEachPropertyGroup(p, "'%24A' == 'D'");

            List<string> conditions = GetListOfBuildPropertyGroupConditions(p);

            Assertion.AssertEquals("'%24A' == 'D'", conditions[0].ToString());
        }
        #endregion

        #region IsImported Tests
        /// <summary>
        /// Tests BuildPropertyGroup IsImported when all BuildPropertyGroups are defined within main project
        /// </summary>
        [Test]
        public void IsImportedAllFromMainProject()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <n1>v1</n1>
                        <n2>v2</n2>
                        <n3 Condition='true'>v3</n3>
                    </PropertyGroup>
                    <PropertyGroup>
                        <n4>v4</n4>
                    </PropertyGroup>
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);

            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                Assertion.AssertEquals(false, group.IsImported);
            }
        }

        /// <summary>
        /// Tests BuildPropertyGroup IsImported when all BuildPropertyGroups are defined within an import
        /// </summary>
        [Test]
        public void IsImportedAllFromImportProject()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                Assertion.AssertEquals(true, group.IsImported);
            }
        }

        /// <summary>
        /// Tests BuildPropertyGroup IsImported when BuildPropertyGroups are defined within main project and an import
        /// </summary>
        [Test]
        public void IsImportedFromMainProjectAndImported()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <n1>v1</n1>
                        <n2>v2</n2>
                        <n3 Condition='true'>v3</n3>
                    </PropertyGroup>
                    <PropertyGroup>
                        <n4>v4</n4>
                    </PropertyGroup>
                    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);

            Assertion.AssertEquals(false, IsPropertyImported(p, "n1"));
            Assertion.AssertEquals(false, IsPropertyImported(p, "n2"));
            Assertion.AssertEquals(false, IsPropertyImported(p, "n3"));
            Assertion.AssertEquals(false, IsPropertyImported(p, "n4"));
            Assertion.AssertEquals(true, IsPropertyImported(p, "OutDir"));
            Assertion.AssertEquals(true, IsPropertyImported(p, "ProjectExt"));
            Assertion.AssertEquals(true, IsPropertyImported(p, "DefaultLanguageSourceExtension"));
        }

        /// <summary>
        /// Tests BuildPropertyGroup IsImported when BuildPropertyGroups are Virtual Property groups
        /// </summary>
        [Test]
        public void IsImportedWithVirtualPropertyGroups()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");

            foreach (BuildProperty property in group)
            {
                Assertion.AssertEquals(false, property.IsImported);
            }
        }
        #endregion

        #region SetImportedPropertyGroupCondition tests
        /// <summary>
        /// Tests BuildPropertyGroup SetImportedPropertyGroupCondition setting to true
        /// </summary>
        [Test]
        public void SetImportedPropertyGroupConditionTrue()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.SetImportedPropertyGroupCondition("true");
                Assertion.AssertEquals("true", group.Condition);
            }
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetImportedPropertyGroupCondition setting to false
        /// </summary>
        [Test]
        public void SetImportedPropertyGroupConditionFalse()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.SetImportedPropertyGroupCondition("false");
                Assertion.AssertEquals("false", group.Condition);
            }
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetImportedPropertyGroupCondition setting general
        /// </summary>
        [Test]
        public void SetImportedPropertyGroupCondition()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.SetImportedPropertyGroupCondition("'$(C)' == 'D'");
                Assertion.AssertEquals("'$(C)' == 'D'", group.Condition);
            }
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetImportedPropertyGroupCondition setting to empty string
        /// </summary>
        [Test]
        public void SetImportedPropertyGroupConditionToEmtpySTring()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.SetImportedPropertyGroupCondition(String.Empty);
                Assertion.AssertEquals(String.Empty, group.Condition);
            }
        }

        /// <summary>
        /// Tests BuildPropertyGroup SetImportedPropertyGroupCondition setting to null
        /// </summary>
        [Test]
        public void SetImportedPropertyGroupConditionToNull()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets' />
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.SetImportedPropertyGroupCondition(null);
                Assertion.AssertEquals(String.Empty, group.Condition);
            }
        }
        #endregion

        #region this[string propertyName] { get; set; } Tests
        /// <summary>
        /// Tests BuildPropertyGroup this[string propertyName] Get
        /// </summary>
        [Test]
        public void ThisGet()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v1");
            group.SetProperty("n2", "v2");

            Assertion.AssertEquals("v1", group["n1"].FinalValue);
            Assertion.AssertEquals("v2", group["n2"].FinalValue);
        }

        /// <summary>
        /// Tests BuildPropertyGroup this[string propertyName] Set
        /// </summary>
        [Test]
        public void ThisSet()
        {
            BuildPropertyGroup group = new BuildPropertyGroup();
            group.SetProperty("n1", "v");
            group.SetProperty("n2", group["n1"].FinalValue);
            group["n1"].Value = "value";

            Assertion.AssertEquals("value", group["n1"].FinalValue);
        }
        #endregion

        #region BuildPropertyGroup Helpers
        /// <summary>
        /// Tells you if your specified BuildProperty name within your Project is imported or not
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="propertyNameWanted">BuildProperty name</param>
        /// <returns>true if specified BuildProject name is in a BuildPropertyGroup that is imported, false if not</returns>
        private static bool IsPropertyImported(Project p, string propertyNameWanted)
        {
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                foreach (BuildProperty property in group)
                {
                    if (String.Equals(property.Name, propertyNameWanted, StringComparison.OrdinalIgnoreCase))
                    {
                        return group.IsImported;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a Dictionary List of properties in your project (Key = property.Name, Value = propery.Value)
        /// </summary>
        /// <param name="p">Project</param>
        /// <returns>A Dictionary List of properties in your project (Key = property.Name, Value = propery.Value)</returns>
        private static Dictionary<string, string> GetDictionaryOfPropertiesInProject(Project p)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                foreach (BuildProperty prop in group)
                {
                    properties.Add(prop.Name, prop.Value);
                }
            }

            return properties;
        }

        /// <summary>
        /// Gets a List of all BuildProperties within your Project
        /// </summary>
        /// <param name="p">Project</param>
        /// <returns>A List of strings of all Build Properties</returns>
        private static List<string> GetListOfBuildProperties(Project p)
        {
            List<string> properties = new List<string>();
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                foreach (BuildProperty property in group)
                {
                    properties.Add(property.Name);
                }
            }

            return properties;
        }

        /// <summary>
        /// Gets a List of all BuildPropertyGroup conditions in your Project
        /// </summary>
        /// <param name="p">Project</param>
        /// <returns>A List of strings of all Build Property Group conditions</returns>
        private static List<string> GetListOfBuildPropertyGroupConditions(Project p)
        {
            List<string> conditions = new List<string>();
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                conditions.Add(group.Condition);
            }

            return conditions;
        }

        /// <summary>
        /// Helper method to set a PropertyGroup condition on all PropertyGroups within a Project
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="condition">The condition you want to set, example "'$(C)' == 'D'"</param>
        private static void SetPropertyGroupConditionOnEachPropertyGroup(Project p, string condition)
        {
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.Condition = condition;
            }
        }

        /// <summary>
        /// Helper method to add new property to BuildPropertyGroups within your project
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="name">String of the property name</param>
        /// <param name="value">String of the property value</param>
        private static void AddNewPropertyToEachPropertyGroup(Project p, string name, string value)
        {
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.AddNewProperty(name, value);
            }
        }

        /// <summary>
        /// Helper method to add new property to BuildPropertyGroups within your project where you can set Property Value as Literal or not
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="name">String of the property name</param>
        /// <param name="value">String of the property value</param>
        /// <param name="treatPropertyValueAsLiteral">true or false</param>
        private static void AddNewPropertyToEachPropertyGroupWithPropertyValueAsLiteral(Project p, string name, string value, bool treatPropertyValueAsLiteral)
        {
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                group.AddNewProperty(name, value, treatPropertyValueAsLiteral);
            }
        }

        /// <summary>
        /// Gets a specific BuildProperty out of your BuildPropertyGroup
        /// </summary>
        /// <param name="group">Your BuildPropertyGroup</param>
        /// <param name="propertyNameWanted">The BuildProperty name that you want</param>
        /// <returns>The BuildProperty of the BuildProperty name you requested</returns>
        private static BuildProperty GetSpecificBuildPropertyOutOfBuildPropertyGroup(BuildPropertyGroup group, string propertyNameWanted)
        {
            foreach (BuildProperty property in group)
            {
                if (String.Equals(property.Name, propertyNameWanted, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }
        #endregion
    }
}
