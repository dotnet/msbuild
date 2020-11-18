// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests.Globbing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using NodeLoggingContext = Microsoft.Build.BackEnd.Logging.NodeLoggingContext;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class IntrinsicTask_Tests
    {
        [Fact]
        public void PropertyGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup> 
                    <p1>v1</p1>
                    <p2>v2</p2>
                </PropertyGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assert.Equal(2, properties.Count);
            Assert.Equal("v1", properties["p1"].EvaluatedValue);
            Assert.Equal("v2", properties["p2"].EvaluatedValue);
        }

        [Fact]
        public void PropertyGroupWithComments()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t' >
                <PropertyGroup><!-- c -->
                    <p1>v1</p1><!-- c -->
                </PropertyGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assert.Single(properties);
            Assert.Equal("v1", properties["p1"].EvaluatedValue);
        }

        [Fact]
        public void PropertyGroupEmpty()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t' >
                <PropertyGroup/>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assert.Empty(properties);
        }

        [Fact]
        public void PropertyGroupWithReservedProperty()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup>
                  <MSBuildProjectFile/>
                </PropertyGroup>
            </Target>
            </Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task);
            }
           );
        }

        [Fact]
        public void PropertyGroupWithInvalidPropertyName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(
                @"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup>
                  <PropertyGroup/>
                </PropertyGroup>
            </Target>
            </Project>"
                );
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task);
            }
           );
        }
        [Fact]
        public void BlankProperty()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup>
                  <p1></p1>
                </PropertyGroup>
            </Target>
            </Project>"
            );
            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assert.Single(properties);
            Assert.Equal("", properties["p1"].EvaluatedValue);
        }

        [Fact]
        public void PropertyGroupWithInvalidSyntax1()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(
                @"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup>x</PropertyGroup>
            </Target>
            </Project>"
                );
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task, null);
            }
           );
        }

        [Fact]
        public void PropertyGroupWithInvalidSyntax2()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(
                @"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup>
                    <p Include='v0'/>
                </PropertyGroup>
            </Target>
            </Project>"
                );
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task, null);
            }
           );
        }
        [Fact]
        public void PropertyGroupWithConditionOnGroup()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup Condition='false'> 
                    <p1>v1</p1>
                    <p2>v2</p2>
                </PropertyGroup>
                <Message Text='[$(P1)][$(P2)]'/>
            </Target>
            </Project>"))));

            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogDoesntContain("[v1][v2]");
            logger.ClearLog();

            p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup Condition='true'> 
                    <p1>v1</p1>
                    <p2>v2</p2>
                </PropertyGroup>
                <Message Text='[$(P1)][$(P2)]'/>
            </Target>
            </Project>"))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogContains("[v1][v2]");
        }

        [Fact]
        public void PropertyGroupWithConditionOnGroupUsingMetadataErrors()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup Condition=""'%(i0.m)'=='m2'"">
                    <p1>@(i0)</p1>
                    <p2>%(i0.m)</p2>
                </PropertyGroup>
            </Target>
            </Project>"))));

            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogContains("MSB4191"); // Metadata not allowed
        }

        [Fact]
        public void ItemGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i2 Include='b1'/>
                </ItemGroup>
            </Target>
            </Project>");

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");
            Assert.Equal("a1", i1Group.First().EvaluatedInclude);
            Assert.Equal("b1", i2Group.First().EvaluatedInclude);
        }

        internal const string TargetitemwithIncludeAndExclude = @"
                    <Project>
                       <Target Name=`t`>
                          <ItemGroup>
                              <i Include='{0}' Exclude='{1}'/>
                          </ItemGroup>
                       </Target>
                    </Project>
                ";

        public static IEnumerable<object[]> IncludesAndExcludesWithWildcardsTestData => GlobbingTestData.IncludesAndExcludesWithWildcardsTestData;

        [Theory]
        [MemberData(nameof(IncludesAndExcludesWithWildcardsTestData))]
        public void ItemsWithWildcards(string includeString, string excludeString, string[] inputFiles, string[] expectedInclude, bool makeExpectedIncludeAbsolute)
        {
            var projectContents = string.Format(TargetitemwithIncludeAndExclude, includeString, excludeString).Cleanup();

            AssertItemEvaluationFromTarget(projectContents, "t", "i", inputFiles, expectedInclude, makeExpectedIncludeAbsolute, normalizeSlashes: true);
        }

        [Fact]
        public void ItemKeepDuplicatesEmptySameAsTrue()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i1 Include='a1' KeepDuplicates='' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i1");
            Assert.Equal(2, group.Count);
        }

        [Fact]
        public void ItemKeepDuplicatesFalse()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i1 Include='a1' KeepDuplicates='false' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i1");
            Assert.Single(group);
        }

        [Fact]
        public void ItemKeepDuplicatesAsCondition()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i1 Include='a1' KeepDuplicates="" '$(Keep)' == 'true' "" />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i1");
            Assert.Single(group);
        }

        [Fact]
        public void ItemKeepDuplicatesFalseKeepsExistingDuplicates()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i1 Include='a1'/>              
                    <i1 Include='a1' KeepDuplicates='false' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i1");
            Assert.Equal(2, group.Count);
        }

        [Fact]
        public void ItemKeepDuplicatesFalseDuringCopyEliminatesDuplicates()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i1 Include='a1'/>              
                    <i2 Include='@(i1)' KeepDuplicates='false' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i1");
            Assert.Equal(2, group.Count);

            group = lookup.GetItems("i2");
            Assert.Single(group);
        }

        [Fact]
        public void ItemKeepDuplicatesFalseWithMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                    </i1>
                    <i1 Include='a2' KeepDuplicates='false' />
                    <i1 Include='a1' KeepDuplicates='false'>
                      <m1>m1</m1>
                    </i1>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i1");
            Assert.Equal(2, group.Count);
        }

        [Fact]
        public void ItemKeepMetadataEmptySameAsKeepAll()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                    </i1>
                    <i2 Include='@(i1)' KeepMetadata='' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i2");
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));
        }

        [Fact]
        public void ItemKeepMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' KeepMetadata='m2' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i2");
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal("m2", group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));
        }


        [Fact]
        public void ItemKeepMetadataNotExistant()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' KeepMetadata='NONEXISTANT' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i2");
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));
        }

        [Fact]
        public void ItemKeepMetadataList()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' KeepMetadata='m1;m2' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i2");
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));
            Assert.Equal("m2", group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));
        }

        [Fact]
        public void ItemKeepMetadataListExpansion()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' KeepMetadata='$(Keep)' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            var scope = lookup.EnterScope("test");
            lookup.SetProperty(ProjectPropertyInstance.Create("Keep", "m1;m2"));
            ExecuteTask(task, lookup);
            scope.LeaveScope();

            var group = lookup.GetItems("i2");
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));
            Assert.Equal("m2", group.First().GetMetadataValue("m2"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m3"));
        }

        [Fact]
        public void ItemRemoveMetadataEmptySameAsKeepAll()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                    </i1>
                    <i2 Include='@(i1)' RemoveMetadata='' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i2");
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));
        }

        [Fact]
        public void ItemRemoveMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' RemoveMetadata='m2' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i2");
            Assert.Equal("m1", group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));
        }

        [Fact]
        public void ItemRemoveMetadataList()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' RemoveMetadata='m1;m2' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var group = lookup.GetItems("i2");
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));
        }

        [Fact]
        public void ItemRemoveMetadataListExpansion()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' RemoveMetadata='$(Remove)' />
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            var scope = lookup.EnterScope("test");
            lookup.SetProperty(ProjectPropertyInstance.Create("Remove", "m1;m2"));
            ExecuteTask(task, lookup);
            scope.LeaveScope();

            var group = lookup.GetItems("i2");
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m1"));
            Assert.Equal(String.Empty, group.First().GetMetadataValue("m2"));
            Assert.Equal("m3", group.First().GetMetadataValue("m3"));
        }

        [Fact]
        public void ItemKeepMetadataAndRemoveMetadataMutuallyExclusive()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>              
                      <m1>m1</m1>
                      <m2>m2</m2>
                      <m3>m3</m3>
                    </i1>
                    <i2 Include='@(i1)' KeepMetadata='m1' RemoveMetadata='m2' />
                </ItemGroup>
            </Target>
            </Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                Lookup lookup = LookupHelpers.CreateEmptyLookup();
                ExecuteTask(task, lookup);
            }
           );
        }
        /// <summary>
        /// Should not make items with an empty include.
        /// </summary>
        [Fact]
        public void ItemGroupWithPropertyExpandingToNothing()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='$(xxx)'/>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            Assert.Empty(i1Group);
        }

        [Fact]
        public void ItemGroupWithComments()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> <!-- c -->
                    <i1 Include='a1;a2'/> <!-- c -->
                    <ii Remove='a1'/> <!-- c -->
                    <i1> <!-- c -->
                        <m>m1</m> <!-- c -->
                    </i1> <!-- c -->
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            Assert.Equal("a1", i1Group.First().EvaluatedInclude);
            Assert.Equal("m1", i1Group.First().GetMetadataValue("m"));
        }

        /// <summary>
        /// This is something that used to be done by CreateItem
        /// </summary>
        [Fact]
        public void ItemGroupTrims()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='  $(p0)  '/>
                    <i2 Include='b1'/>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            properties.Set(ProjectPropertyInstance.Create("p0", "    v0    "));
            Lookup lookup = LookupHelpers.CreateLookup(properties);
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            Assert.Equal("v0", i1Group.First().EvaluatedInclude);
        }

        [Fact]
        public void ItemGroupWithInvalidSyntax1()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>x</ItemGroup>
            </Target>
            </Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task, null);
            }
           );
        }

        [Fact]
        public void ItemGroupWithInvalidSyntax2()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                  <i>x</i>
                </ItemGroup>
            </Target>
            </Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task, null);
            }
           );
        }

        [Fact]
        public void ItemGroupWithInvalidSyntax3()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                  <i Include='x' Exclude='y' Remove='z'/>
                </ItemGroup>
            </Target>
            </Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task, null);
            }
           );
        }
        [Fact]
        public void ItemGroupWithTransform()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a.cpp'/>
                    <i2 Include=""@(i1->'%(filename).obj')""/>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");
            Assert.Equal("a.cpp", i1Group.First().EvaluatedInclude);
            Assert.Equal("a.obj", i2Group.First().EvaluatedInclude);
        }

        [Fact]
        public void ItemGroupWithTransformInMetadataValue()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a.cpp'/>
                    <i2 Include='@(i1)'>
                       <m>@(i1->'%(filename).obj')</m>
                    </i2>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");
            Assert.Equal("a.cpp", i2Group.First().EvaluatedInclude);
            Assert.Equal("a.obj", i2Group.First().GetMetadataValue("m"));
        }

        [Fact]
        public void ItemGroupWithExclude()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i2 Include='a1;@(i1);b1;b2' Exclude='@(i1);b1'/>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");
            Assert.Equal("a1", i1Group.First().EvaluatedInclude);
            Assert.Equal("b2", i2Group.First().EvaluatedInclude);
        }

        [Fact]
        public void ItemGroupWithMetadataInExclude()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>
                        <m>a1</m>
                    </i1>
                    <i2 Include='b1;@(i1)' Exclude='%(i1.m)'/>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");
            Assert.Single(i1Group);
            Assert.Single(i2Group);
            Assert.Equal("a1", i1Group.First().EvaluatedInclude);
            Assert.Equal("b1", i2Group.First().EvaluatedInclude);
        }

        [Fact]
        public void ItemGroupWithConditionOnGroup()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup Condition='false'> 
                    <i1 Include='a1'/>
                    <i2 Include='b1'/>
                </ItemGroup>
                <Message Text='[@(i1)][@(i2)]'/>
            </Target>
            </Project>"))));

            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogDoesntContain("[a1][b1]");
            logger.ClearLog();

            p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup Condition='true'> 
                    <i1 Include='a1'/>
                    <i2 Include='b1'/>
                </ItemGroup>
                <Message Text='[@(i1)][@(i2)]'/>
            </Target>
            </Project>"))));

            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogContains("[a1][b1]");
        }

        [Fact]
        public void ItemGroupWithConditionOnGroupUsingMetadataErrors()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup Condition=""'%(i0.m)'!='m1'"">
                    <i1 Include='a1'/>
                    <i2 Include='%(i0.m)'/>
                    <i3 Include='%(i0.identity)'/>
                    <i4 Include='@(i0)'/>
                </ItemGroup>
            </Target>
            </Project>"))));

            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogContains("MSB4191"); // Metadata not allowed
        }

        [Fact]
        public void PropertyGroupWithExternalPropertyReferences()
        {
            // <PropertyGroup>
            //     <p0>v0</p0>
            // </PropertyGroup>
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup> 
                    <p1>$(p0)</p1>
                </PropertyGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = GeneratePropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assert.Equal(2, properties.Count);
            Assert.Equal("v0", properties["p0"].EvaluatedValue);
            Assert.Equal("v0", properties["p1"].EvaluatedValue);
        }

        [Fact]
        public void ItemGroupWithPropertyReferences()
        {
            // <PropertyGroup>
            //     <p0>v0</p0>
            // </PropertyGroup>
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='$(p0)'/>
                    <i2 Include='a2'/>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = GeneratePropertyGroup();
            Lookup lookup = LookupHelpers.CreateLookup(properties);
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");
            Assert.Equal("v0", i1Group.First().EvaluatedInclude);
            Assert.Equal("a2", i2Group.First().EvaluatedInclude);
        }

        [Fact]
        public void ItemGroupWithMetadataReferences()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>
                        <m>m1</m>
                    </i1>
                    <i1 Include='a2'>
                        <m>m2</m>
                    </i1>
                    <i2 Include='%(i1.m)'/>
                </ItemGroup>
            </Target>
            </Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");

            Assert.Equal("a1", i1Group.First().EvaluatedInclude);
            Assert.Equal("a2", i1Group.ElementAt(1).EvaluatedInclude);
            Assert.Equal("m1", i2Group.First().EvaluatedInclude);
            Assert.Equal("m2", i2Group.ElementAt(1).EvaluatedInclude);

            Assert.Equal("m1", i1Group.First().GetMetadataValue("m"));
            Assert.Equal("m2", i1Group.ElementAt(1).GetMetadataValue("m"));
        }

        [Fact]
        public void ItemGroupWithMetadataReferencesOnMetadataConditions()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='a1'>
                        <m>m1</m>
                    </i1>
                    <i1 Include='a2'>
                        <m>m2</m>
                    </i1>
                    <i2 Include='@(i1)'>
                        <n Condition=""'%(i1.m)'=='m1'"">n1</n>
                    </i2>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");

            Assert.Equal(2, i2Group.Count);
            Assert.Equal("a1", i2Group.First().EvaluatedInclude);
            Assert.Equal("a2", i2Group.ElementAt(1).EvaluatedInclude);

            Assert.Equal("n1", i2Group.First().GetMetadataValue("n"));
            Assert.Equal(String.Empty, i2Group.ElementAt(1).GetMetadataValue("n"));
        }

        [Fact]
        public void ItemGroupWithMetadataReferencesOnItemGroupAndItemConditionsErrors()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup Condition=""'%(i0.m)' != m1"" >
                    <i1 Include=""%(m)"" Condition=""'%(i0.m)' != m3""/>
                </ItemGroup>
            </Target></Project>"))));

            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogContains("MSB4191"); // Metadata not allowed
        }

        [Fact]
        public void ItemGroupWithExternalMetadataReferences()
        {
            // <ItemGroup>
            //    <i0 Include='a1'>
            //        <m>m1</m>
            //    </i0>
            //    <i0 Include='a2;a3'>
            //        <m>m2</m>
            //    </i0>
            //    <i0 Include='a4'>
            //        <m>m3</m>
            //    </i0>
            // </ItemGroup>
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include='b1'>
                        <m>%(i0.m)</m>
                    </i1>
                    <i2 Include='%(i1.m)'/>
                </ItemGroup>
            </Target></Project>");

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup(task.Project);
            ExecuteTask(task, lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");
            ICollection<ProjectItemInstance> i2Group = lookup.GetItems("i2");

            Assert.Equal("b1", i1Group.First().EvaluatedInclude);
            Assert.Equal("b1", i1Group.ElementAt(1).EvaluatedInclude);
            Assert.Equal("b1", i1Group.ElementAt(2).EvaluatedInclude);
            Assert.Equal("m1", i1Group.First().GetMetadataValue("m"));
            Assert.Equal("m2", i1Group.ElementAt(1).GetMetadataValue("m"));
            Assert.Equal("m3", i1Group.ElementAt(2).GetMetadataValue("m"));

            Assert.Equal("m1", i2Group.First().EvaluatedInclude);
            Assert.Equal("m2", i2Group.ElementAt(1).EvaluatedInclude);
            Assert.Equal("m3", i2Group.ElementAt(2).EvaluatedInclude);
        }

        [Fact]
        public void PropertyGroupWithCumulativePropertyReferences()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup> 
                    <p1>v1</p1>
                    <p2>#$(p1)#</p2>
                    <p1>v2</p1>
                </PropertyGroup>
            </Target></Project>");

            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assert.Equal(2, properties.Count);
            Assert.Equal("v2", properties["p1"].EvaluatedValue);
            Assert.Equal("#v1#", properties["p2"].EvaluatedValue);
        }

        [Fact]
        public void PropertyGroupWithMetadataReferencesOnGroupErrors()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup Condition=""'%(i0.m)' != m1"">
                    <p1>%(i0.m)</p1>
                </PropertyGroup>
            </Target></Project>"))));

            p.Build(new string[] { "t" }, new ILogger[] { logger });
            logger.AssertLogContains("MSB4191");
        }

        [Fact]
        public void PropertyGroupWithMetadataReferencesOnProperty()
        {
            // <ItemGroup>
            //    <i0 Include='a1'>
            //        <m>m1</m>
            //        <n>n1</n>
            //    </i0>
            //    <i0 Include='a2;a3'>
            //        <m>m2</m>
            //        <n>n2</n>
            //    </i0>
            //    <i0 Include='a4'>
            //        <m>m3</m>
            //        <n>n3</n>
            //    </i0>
            // </ItemGroup>
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup> 
                    <p1 Condition=""'%(i0.n)' != n3"">%(i0.n)</p1>
                </PropertyGroup>
            </Target></Project>");

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup(task.Project);
            ExecuteTask(task, lookup);

            Assert.Equal("n2", lookup.GetProperty("p1").EvaluatedValue);
        }

        [Fact]
        public void PropertiesCanReferenceItemsInSameTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t'>
                    <ItemGroup>
                      <i1 Include='a1;a2'/>
                    </ItemGroup>
                    <PropertyGroup>
                      <p>@(i1->'#%(identity)#', '*')</p>
                    </PropertyGroup>
                    <Message Text='[$(p)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[#a1#*#a2#]");
        }

        [Fact]
        public void ItemsCanReferencePropertiesInSameTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t'>
                    <PropertyGroup>
                        <p0>v0</p0>
                    </PropertyGroup>
                    <ItemGroup> 
                        <i1 Include='$(p0)'/>
                    </ItemGroup>
                    <Message Text='[@(i1)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[v0]");
        }

        [Fact]
        public void PropertyGroupInTargetCanOverwriteGlobalProperties()
        {
            MockLogger logger = new MockLogger();
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties.Add("global", "v0");

            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <PropertyGroup>
                    <global>v1</global>
                  </PropertyGroup>
                  <Target Name='t2' DependsOnTargets='t'>
                    <Message Text='final:[$(global)]'/>
                  </Target>
                  <Target Name='t'>
                    <Message Text='start:[$(global)]'/>
                    <PropertyGroup>
                      <global>v2</global>
                    </PropertyGroup>
                    <Message Text='end:[$(global)]'/>
                  </Target>
                </Project>
            "))), globalProperties, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            ProjectInstance p = project.CreateProjectInstance();

            Assert.Equal("v0", p.GetProperty("global").EvaluatedValue);
            p.Build(new string[] { "t2" }, new ILogger[] { logger });

            // PropertyGroup outside of target can't overwrite global property,
            // but PropertyGroup inside of target can overwrite it
            logger.AssertLogContains("start:[v0]", "end:[v2]", "final:[v2]");
            Assert.Equal("v2", p.GetProperty("global").EvaluatedValue);

            // Resetting the project goes back to the old value
            p = project.CreateProjectInstance();
            Assert.Equal("v0", p.GetProperty("global").EvaluatedValue);
        }

        [Fact]
        public void PropertiesAreRevertedAfterBuild()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <PropertyGroup>
                    <p>p0</p>
                  </PropertyGroup>
                  <Target Name='t'>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                  </Target>
                </Project>
            "))));

            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            string value = p.GetProperty("p").EvaluatedValue;
            Assert.Equal("p1", value);

            p = project.CreateProjectInstance();

            value = p.GetProperty("p").EvaluatedValue;
            Assert.Equal("p0", value);
        }

        [Fact]
        public void PropertiesVisibleToSubsequentTask()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t'>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                    <Message Text='[$(p)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[p1]");
        }

        [Fact]
        public void PropertiesVisibleToSubsequentTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t2' DependsOnTargets='t'>
                    <Message Text='[$(p)]'/>                    
                  </Target>
                  <Target Name='t'>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t2" }, new ILogger[] { logger });

            logger.AssertLogContains("[p1]");
        }

        [Fact]
        public void ItemsVisibleToSubsequentTask()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t'>
                    <ItemGroup>
                      <i Include='i1'/>
                    </ItemGroup>
                    <Message Text='[@(i)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[i1]");
        }

        [Fact]
        public void ItemsVisibleToSubsequentTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t2' DependsOnTargets='t'>
                    <Message Text='[@(i)]'/>                    
                  </Target>
                  <Target Name='t'>
                    <ItemGroup>
                      <i Include='i1'/>
                    </ItemGroup>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t2" }, new ILogger[] { logger });

            logger.AssertLogContains("[i1]");
        }

        [Fact]
        public void ItemsNotVisibleToParallelTargetBatches()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='1.in'><output>1.out</output></i>
                    <i Include='2.in'><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.output)'>
                    <Message Text='start:[@(i)]'/>
                    <ItemGroup>
                      <j Include='%(i.identity)'/>
                    </ItemGroup>
                    <Message Text='end:[@(j)]'/>                    
                </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "start:[1.in]", "end:[1.in]", "start:[2.in]", "end:[2.in]" });
        }

        [Fact]
        public void PropertiesNotVisibleToParallelTargetBatches()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='1.in'><output>1.out</output></i>
                    <i Include='2.in'><output>2.out</output></i>
                  </ItemGroup>
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.output)'>
                    <Message Text='start:[$(p)]'/>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                    <Message Text='end:[$(p)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "start:[]", "end:[p1]", "start:[]", "end:[p1]" });
        }

        // One input is built, the other is inferred
        [Fact]
        public void ItemsInPartialBuild()
        {
            string[] oldFiles = null, newFiles = null;
            try
            {
                oldFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));
                newFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2006, 1, 1));

                MockLogger logger = new MockLogger();
                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='" + oldFiles.First() + "'><output>" + newFiles.First() + @"</output></i>
                    <i Include='" + newFiles.ElementAt(1) + "'><output>" + oldFiles.ElementAt(1) + @"</output></i>
                  </ItemGroup>
                  <Target Name='t2' DependsOnTargets='t'>
                    <Message Text='final:[@(j)]'/>
                  </Target>
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.Output)'>
                    <Message Text='start:[@(j)]'/>
                    <ItemGroup>
                      <j Include='%(i.identity)'/>
                    </ItemGroup>
                    <Message Text='end:[@(j)]'/>
                </Target>
                </Project>
            "))));
                p.Build(new string[] { "t2" }, new ILogger[] { logger });

                // We should only see messages for the out of date inputs, but the itemgroup should do its work for both inputs
                logger.AssertLogContains(new string[] { "start:[]", "end:[" + newFiles.ElementAt(1) + "]", "final:[" + oldFiles.First() + ";" + newFiles.ElementAt(1) + "]" });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(oldFiles);
                ObjectModelHelpers.DeleteTempFiles(newFiles);
            }
        }

        // One input is built, the other input is inferred
        [Fact]
        public void PropertiesInPartialBuild()
        {
            string[] oldFiles = null, newFiles = null;
            try
            {
                oldFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));
                newFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2006, 1, 1));

                MockLogger logger = new MockLogger();
                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='" + oldFiles.First() + "'><output>" + newFiles.First() + @"</output></i>
                    <i Include='" + newFiles.ElementAt(1) + "'><output>" + oldFiles.ElementAt(1) + @"</output></i>
                  </ItemGroup>
                  <Target Name='t2' DependsOnTargets='t'>
                    <Message Text='final:[$(p)]'/>
                  </Target>
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.Output)'>
                    <Message Text='start:[$(p)]'/>
                    <PropertyGroup>
                      <p>@(i)</p>
                    </PropertyGroup>
                    <Message Text='end:[$(p)]'/>
                </Target>
                </Project>
            "))));
                p.Build(new string[] { "t2" }, new ILogger[] { logger });

                // We should only see messages for the out of date inputs, but the propertygroup should do its work for both inputs
                // Finally, execution wins over inferral, as the code chooses to do it that way
                logger.AssertLogContains(new string[] { "start:[]", "end:[" + newFiles.ElementAt(1) + "]", "final:[" + newFiles.ElementAt(1) + "]" });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(oldFiles);
                ObjectModelHelpers.DeleteTempFiles(newFiles);
            }
        }

        // One input is built, the other is inferred
        [Fact]
        public void ItemsInPartialBuildVisibleToSubsequentlyInferringTasks()
        {
            string[] oldFiles = null, newFiles = null;
            try
            {
                oldFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));
                newFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2006, 1, 1));
                string oldInput = oldFiles.First();
                string newInput = newFiles.ElementAt(1);
                string oldOutput = oldFiles.ElementAt(1);
                string newOutput = newFiles.First();

                MockLogger logger = new MockLogger();
                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='" + oldInput + "'><output>" + newOutput + @"</output></i>
                    <i Include='" + newInput + "'><output>" + oldOutput + @"</output></i>
                  </ItemGroup>
                  <Target Name='t2' DependsOnTargets='t'>
                    <Message Text='final:[@(i)]'/>
                  </Target>
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.Output)'>
                    <Message Text='start:[@(i)]'/>
                    <ItemGroup>
                      <j Include='%(i.identity)'/>
                    </ItemGroup>
                    <Message Text='middle:[@(i)][@(j)]'/>
                    <CreateItem Include='@(j)'>
                      <Output TaskParameter='Include' ItemName='i'/>
                    </CreateItem>
                    <Message Text='end:[@(i)]'/>
                </Target>
                </Project>
            "))));
                p.Build(new string[] { "t2" }, new ILogger[] { logger });

                // We should only see messages for the out of date inputs, but the itemgroup should do its work for both inputs;
                // The final result should include the out of date inputs (twice) and the up to date inputs (twice).
                // NOTE: outputs from regular tasks, like CreateItem, are gathered up and included in the project in the order (1) inferred (2) executed.
                // Intrinsic tasks, because they affect the project directly, don't do this. So the final order we see is
                // two inputs (old, new) from the ItemGroup; followed by the inferred CreateItem output, then the executed CreateItem output.
                // I suggest this ordering isn't important: it's a new feature, so nobody will get broken.
                logger.AssertLogContains(new string[] { "start:[" + newInput + "]",
                                                        "middle:[" + newInput + "][" + newInput + "]",
                                                        "end:["   + newInput + ";" + newInput + "]",
                                                        "final:[" + oldInput + ";" + newInput + ";" + oldInput + ";" + newInput + "]" });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(oldFiles);
                ObjectModelHelpers.DeleteTempFiles(newFiles);
            }
        }

        [Fact]
        public void IncludeNoOp()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Include=''/>
                </ItemGroup>
            </Target></Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task, null);
            }
           );
        }
        [Fact]
        public void RemoveNoOp()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1 Remove='a1'/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assert.Empty(lookup.GetItems("i1"));
        }

        [Fact]
        public void RemoveItemInTarget()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'/> 
                    <i1 Remove='a1'/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assert.Empty(lookup.GetItems("i1"));
        }

        /// <summary>
        /// Removes in one batch should never affect adds in a parallel batch, even if that
        /// parallel batch ran first.
        /// </summary>
        [Fact]
        public void RemoveOfItemAddedInTargetByParallelTargetBatchDoesNothing()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <!-- just to cause two target batches -->
                    <i Include='1.in'><output>1.out</output></i>
                    <i Include='2.in'><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.output)'>
                    <ItemGroup>
                      <j Include='a' Condition=""'%(i.Identity)'=='1.in'""/>
                      <j Remove='a' Condition=""'%(i.Identity)'=='2.in'""/>

                      <!-- and again in reversed batch order, in case the engine batches the other way around -->
                      <j Include='b' Condition=""'%(i.Identity)'=='2.in'""/>
                      <j Remove='b' Condition=""'%(i.Identity)'=='1.in'""/>

                      <!-- but obviously a remove in the same batch works -->
                      <j Include='c' Condition=""'%(i.Identity)'=='2.in'""/>
                      <j Remove='c' Condition=""'%(i.Identity)'=='2.in'""/>

                      <!-- unless it's before the add -->
                      <j Remove='d' Condition=""'%(i.Identity)'=='2.in'""/>
                      <j Include='d' Condition=""'%(i.Identity)'=='2.in'""/>
                  </ItemGroup>
                  </Target>
                  <Target Name='t2'>
                    <Message Text='final:[@(j)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t", "t2" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "final:[a;b;d]" });
        }

        [Fact]
        public void RemoveItemInTargetWithTransform()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i0 Include='a.cpp;b.cpp'/>
                    <i1 Include='a.obj;b.obj'/> 
                    <i1 Remove=""@(i0->'%(filename).obj')""/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assert.Empty(lookup.GetItems("i1"));
        }

        [Fact]
        public void RemoveWithMultipleIncludes()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'/> 
                    <i1 Include='a2'/> 
                    <i1 Remove='a1;a2'/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assert.Empty(lookup.GetItems("i1"));
        }

        [Fact]
        public void RemoveAllItemsInList()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'/> 
                    <i1 Include='a2'/> 
                    <i1 Remove='@(i1)'/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assert.Empty(lookup.GetItems("i1"));
        }

        [Fact]
        public void RemoveWithItemReferenceOnMatchingMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' M1='1' M2='a'/>
                            <I1 Include='b1' M1='2' M2='x'/>
                            <I1 Include='c1' M1='3' M2='y'/>
                            <I1 Include='d1' M1='4' M2='b'/>

                            <I2 Include='a2' M1='x' m2='c'/>
                            <I2 Include='b2' M1='2' m2='x'/>
                            <I2 Include='c2' M1='3' m2='Y'/>
                            <I2 Include='d2' M1='y' m2='d'/>

                            <I2 Remove='@(I1)' MatchOnMetadata='m1' />
                        </ItemGroup>
                    </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var items = lookup.GetItems("I2");

            items.Select(i => i.EvaluatedInclude).ShouldBe(new []{"a2", "d2"});

            items.ElementAt(0).GetMetadataValue("M1").ShouldBe("x");
            items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
            items.ElementAt(1).GetMetadataValue("M1").ShouldBe("y");
            items.ElementAt(1).GetMetadataValue("M2").ShouldBe("d");
        }

        [Fact]
        public void RemoveWithItemReferenceOnCaseInsensitiveMatchingMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' M1='1' M2='a'/>
                            <I1 Include='b1' M1='2' M2='x'/>
                            <I1 Include='c1' M1='3' M2='y'/>
                            <I1 Include='d1' M1='4' M2='b'/>

                            <I2 Include='a2' M1='x' m2='c'/>
                            <I2 Include='b2' M1='2' m2='x'/>
                            <I2 Include='c2' M1='3' m2='Y'/>
                            <I2 Include='d2' M1='y' m2='d'/>

                            <I2 Remove='@(I1)' MatchOnMetadata='m2' MatchOnMetadataOptions='CaseInsensitive' />
                        </ItemGroup>
                    </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var items = lookup.GetItems("I2");

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "d2" });

            items.ElementAt(0).GetMetadataValue("M1").ShouldBe("x");
            items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
            items.ElementAt(1).GetMetadataValue("M1").ShouldBe("y");
            items.ElementAt(1).GetMetadataValue("M2").ShouldBe("d");
        }

        [Fact]
        public void RemoveWithItemReferenceOnFilePathMatchingMetadata()
        {
            using (var env = TestEnvironment.Create())
            {
                env.SetCurrentDirectory(Environment.CurrentDirectory);
                string content = ObjectModelHelpers.CleanupFileContents(
                    $@"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' M1='foo.txt\' M2='a'/>
                            <I1 Include='b1' M1='foo/bar.cs' M2='x'/>
                            <I1 Include='c1' M1='foo/bar.vb' M2='y'/>
                            <I1 Include='d1' M1='foo\foo\foo' M2='b'/>
                            <I1 Include='e1' M1='a/b/../c/./d' M2='1'/>
                            <I1 Include='f1' M1='{ Environment.CurrentDirectory }\b\c' M2='6'/>

                            <I2 Include='a2' M1='FOO.TXT' m2='c'/>
                            <I2 Include='b2' M1='foo/bar.txt' m2='x'/>
                            <I2 Include='c2' M1='/foo/BAR.vb\\/' m2='Y'/>
                            <I2 Include='d2' M1='foo/foo/foo/' m2='d'/>
                            <I2 Include='e2' M1='foo/foo/foo/' m2='c'/>
                            <I2 Include='f2' M1='b\c' m2='e'/>
                            <I2 Include='g2' M1='b\d\c' m2='f'/>

                            <I2 Remove='@(I1)' MatchOnMetadata='m1' MatchOnMetadataOptions='PathLike' />
                        </ItemGroup>
                    </Target></Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                Lookup lookup = LookupHelpers.CreateEmptyLookup();
                ExecuteTask(task, lookup);

                var items = lookup.GetItems("I2");

                if (FileUtilities.GetIsFileSystemCaseSensitive())
                {
                    items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "b2", "c2", "g2" });

                    items.ElementAt(0).GetMetadataValue("M1").ShouldBe(@"FOO.TXT");
                    items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
                    items.ElementAt(1).GetMetadataValue("M1").ShouldBe("foo/bar.txt");
                    items.ElementAt(1).GetMetadataValue("M2").ShouldBe("x");
                    items.ElementAt(2).GetMetadataValue("M1").ShouldBe(@"/foo/BAR.vb\\/");
                    items.ElementAt(2).GetMetadataValue("M2").ShouldBe("Y");
                    items.ElementAt(3).GetMetadataValue("M1").ShouldBe(@"b\d\c");
                    items.ElementAt(3).GetMetadataValue("M2").ShouldBe("f");
                }
                else
                {
                    items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "b2", "c2", "g2" });

                    items.ElementAt(0).GetMetadataValue("M1").ShouldBe("foo/bar.txt");
                    items.ElementAt(0).GetMetadataValue("M2").ShouldBe("x");
                    items.ElementAt(1).GetMetadataValue("M1").ShouldBe(@"/foo/BAR.vb\\/");
                    items.ElementAt(1).GetMetadataValue("M2").ShouldBe("Y");
                    items.ElementAt(2).GetMetadataValue("M1").ShouldBe(@"b\d\c");
                    items.ElementAt(2).GetMetadataValue("M2").ShouldBe("f");
                }
            }
        }

        [Fact]
        public void RemoveWithItemReferenceOnIntrinsicMatchingMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                $@"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='foo.txt' />
                            <I1 Include='bar.cs' />
                            <I1 Include='../bar.cs' />
                            <I1 Include='/foo/../bar.txt' />

                            <I2 Include='foo.txt' />
                            <I2 Include='../foo.txt' />
                            <I2 Include='/bar.txt' />
                            <I2 Include='/foo/bar.txt' />

                            <I2 Remove='@(I1)' MatchOnMetadata='FullPath' MatchOnMetadataOptions='PathLike' />
                        </ItemGroup>
                    </Target></Project> ");

            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = GeneratePropertyGroup();
            Lookup lookup = LookupHelpers.CreateLookup(properties);
            ExecuteTask(task, lookup);

            var items = lookup.GetItems("I2");

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "../foo.txt", "/foo/bar.txt" });
        }

        [Fact]
        public void RemoveWithPropertyReferenceInMatchOnMetadata()
        {
            // <PropertyGroup>
            //     <p0>v0</p0>
            // </PropertyGroup>
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' v0='1' M2='a'/>
                            <I1 Include='b1' v0='2' M2='x'/>
                            <I1 Include='c1' v0='3' M2='y'/>
                            <I1 Include='d1' v0='4' M2='b'/>

                            <I2 Include='a2' v0='x' m2='c'/>
                            <I2 Include='b2' v0='2' m2='x'/>
                            <I2 Include='c2' v0='3' m2='Y'/>
                            <I2 Include='d2' v0='y' m2='d'/>

                            <I2 Remove='@(I1)' MatchOnMetadata='$(p0)' />
                        </ItemGroup>
                    </Target></Project>");

            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = GeneratePropertyGroup();
            Lookup lookup = LookupHelpers.CreateLookup(properties);
            ExecuteTask(task, lookup);

            var items = lookup.GetItems("I2");

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "d2" });

            items.ElementAt(0).GetMetadataValue("v0").ShouldBe("x");
            items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
            items.ElementAt(1).GetMetadataValue("v0").ShouldBe("y");
            items.ElementAt(1).GetMetadataValue("M2").ShouldBe("d");
        }

        [Fact]
        public void RemoveWithItemReferenceInMatchOnMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <Meta2 Include='M2'/>

                            <I1 Include='a1' v0='1' M2='a'/>
                            <I1 Include='b1' v0='2' M2='x'/>
                            <I1 Include='c1' v0='3' M2='y'/>
                            <I1 Include='d1' v0='4' M2='b'/>

                            <I2 Include='a2' v0='x' m2='c'/>
                            <I2 Include='b2' v0='2' m2='x'/>
                            <I2 Include='c2' v0='3' m2='Y'/>
                            <I2 Include='d2' v0='y' m2='d'/>

                            <I2 Remove='@(I1)' MatchOnMetadata='@(Meta2)' />
                        </ItemGroup>
                    </Target></Project>");

            IntrinsicTask task = CreateIntrinsicTask(content);
            PropertyDictionary<ProjectPropertyInstance> properties = GeneratePropertyGroup();
            Lookup lookup = LookupHelpers.CreateLookup(properties);
            ExecuteTask(task, lookup);

            var items = lookup.GetItems("I2");

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "c2", "d2" });

            items.ElementAt(0).GetMetadataValue("v0").ShouldBe("x");
            items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
            items.ElementAt(1).GetMetadataValue("v0").ShouldBe("3");
            items.ElementAt(1).GetMetadataValue("M2").ShouldBe("Y");
            items.ElementAt(2).GetMetadataValue("v0").ShouldBe("y");
            items.ElementAt(2).GetMetadataValue("M2").ShouldBe("d");
        }

        [Fact]
        public void KeepWithItemReferenceOnNonmatchingMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' a='1' b='a'/>
                            <I1 Include='b1' a='2' b='x'/>
                            <I1 Include='c1' a='3' b='y'/>
                            <I1 Include='d1' a='4' b='b'/>

                            <I2 Include='a2' c='x' d='c'/>
                            <I2 Include='b2' c='2' d='x'/>
                            <I2 Include='c2' c='3' d='Y'/>
                            <I2 Include='d2' c='y' d='d'/>

                            <I2 Remove='@(I1)' MatchOnMetadata='e' />
                        </ItemGroup>
                    </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            var items = lookup.GetItems("I2");

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "b2", "c2", "d2" });

            items.ElementAt(0).GetMetadataValue("c").ShouldBe("x");
            items.ElementAt(1).GetMetadataValue("c").ShouldBe("2");
            items.ElementAt(2).GetMetadataValue("c").ShouldBe("3");
            items.ElementAt(3).GetMetadataValue("c").ShouldBe("y");
            items.ElementAt(0).GetMetadataValue("d").ShouldBe("c");
            items.ElementAt(1).GetMetadataValue("d").ShouldBe("x");
            items.ElementAt(2).GetMetadataValue("d").ShouldBe("Y");
            items.ElementAt(3).GetMetadataValue("d").ShouldBe("d");
        }

        [Fact]
        public void FailWithMatchingMultipleMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' M1='1' M2='a'/>
                            <I1 Include='b1' M1='2' M2='x'/>
                            <I1 Include='c1' M1='3' M2='y'/>
                            <I1 Include='d1' M1='4' M2='b'/>

                            <I2 Include='a2' M1='x' m2='c'/>
                            <I2 Include='b2' M1='2' m2='x'/>
                            <I2 Include='c2' M1='3' m2='Y'/>
                            <I2 Include='d2' M1='y' m2='d'/>

                            <I2 Remove='@(I1)' MatchOnMetadata='M1;M2' />
                        </ItemGroup>
                    </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            Assert.ThrowsAny<InvalidProjectFileException>(() => ExecuteTask(task, lookup))
                .HelpKeyword.ShouldBe("MSBuild.OM_MatchOnMetadataIsRestrictedToOnlyOneReferencedItem");
        }

        [Fact]
        public void FailWithMultipleItemReferenceOnMatchingMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' M1='1' M2='a'/>
                            <I1 Include='b1' M1='2' M2='x'/>
                            <I1 Include='c1' M1='3' M2='y'/>
                            <I1 Include='d1' M1='4' M2='b'/>

                            <I2 Include='a2' M1='x' m2='c'/>
                            <I2 Include='b2' M1='2' m2='x'/>
                            <I2 Include='c2' M1='3' m2='Y'/>
                            <I2 Include='d2' M1='y' m2='d'/>

                            <I3 Include='a3' M1='1' m2='b'/>
                            <I3 Include='b3' M1='x' m2='a'/>
                            <I3 Include='c3' M1='3' m2='2'/>
                            <I3 Include='d3' M1='y' m2='d'/>

                            <I3 Remove='@(I1);@(I2)' MatchOnMetadata='M1' />
                        </ItemGroup>
                    </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            Assert.ThrowsAny<InvalidProjectFileException>(() => ExecuteTask(task, lookup))
                .HelpKeyword.ShouldBe("MSBuild.OM_MatchOnMetadataIsRestrictedToOnlyOneReferencedItem");
        }

        [Fact]
        public void FailWithMetadataItemReferenceOnMatchingMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <Target Name='t'>
                        <ItemGroup>
                            <I1 Include='a1' M1='1' M2='a'/>
                            <I1 Include='b1' M1='2' M2='x'/>
                            <I1 Include='c1' M1='3' M2='y'/>
                            <I1 Include='d1' M1='4' M2='b'/>

                            <I2 Include='a2' M1='x' m2='c'/>
                            <I2 Include='b2' M1='2' m2='x'/>
                            <I2 Include='c2' M1='3' m2='Y'/>
                            <I2 Include='d2' M1='y' m2='d'/>

                            <I2 Remove='%(I1.M1)' MatchOnMetadata='M1' />
                        </ItemGroup>
                    </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            Assert.ThrowsAny<InvalidProjectFileException>(() => ExecuteTask(task, lookup))
                .HelpKeyword.ShouldBe("MSBuild.OM_MatchOnMetadataIsRestrictedToOnlyOneReferencedItem");
        }

        [Fact]
        public void RemoveItemOutsideTarget()
        {
            // <ItemGroup>
            //    <i0 Include='a1'>
            //        <m>m1</m>
            //        <n>n1</n>
            //    </i0>
            //    <i0 Include='a2;a3'>
            //        <m>m2</m>
            //        <n>n2</n>
            //    </i0>
            //    <i0 Include='a4'>
            //        <m>m3</m>
            //        <n>n3</n>
            //    </i0>
            // </ItemGroup>
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i0 Remove='a2'/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup(task.Project);

            task.ExecuteTask(lookup);

            ICollection<ProjectItemInstance> i0Group = lookup.GetItems("i0");

            Assert.Equal(3, i0Group.Count);
            Assert.Equal("a1", i0Group.First().EvaluatedInclude);
            Assert.Equal("a3", i0Group.ElementAt(1).EvaluatedInclude);
            Assert.Equal("a4", i0Group.ElementAt(2).EvaluatedInclude);
        }

        /// <summary>
        /// Bare (batchable) metadata is prohibited on IG/PG conditions -- all other expressions
        /// should be allowed
        /// </summary>
        [Fact]
        public void ConditionOnPropertyGroupUsingPropertiesAndItemListsAndTransforms()
        {
            // <ItemGroup>
            //    <i0 Include='a1'>
            //        <m>m1</m>
            //    </i0>
            //    <i0 Include='a2;a3'>
            //        <m>m2</m>
            //    </i0>
            //    <i0 Include='a4'>
            //        <m>m3</m>
            //    </i0>
            // </ItemGroup>
            // <PropertyGroup>
            //     <p0>v0</p0>
            // </PropertyGroup>
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <PropertyGroup Condition=""'$(p0)'=='v0' and '@(i0)'=='a1;a2;a3;a4' and '@(i0->'%(identity).x','|')'=='a1.x|a2.x|a3.x|a4.x'"">
                  <p1>v1</p1>
                </PropertyGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookupWithItemsAndProperties(task.Project);

            task.ExecuteTask(lookup);

            string p1 = lookup.GetProperty("p1").EvaluatedValue;

            Assert.Equal("v1", p1);
        }

        /// <summary>
        /// Bare (batchable) metadata is prohibited on IG/PG conditions -- all other expressions
        /// should be allowed
        /// </summary>
        [Fact]
        public void ConditionOnItemGroupUsingPropertiesAndItemListsAndTransforms()
        {
            // <ItemGroup>
            //    <i0 Include='a1'>
            //        <m>m1</m>
            //    </i0>
            //    <i0 Include='a2;a3'>
            //        <m>m2</m>
            //    </i0>
            //    <i0 Include='a4'>
            //        <m>m3</m>
            //    </i0>
            // </ItemGroup>
            // <PropertyGroup>
            //     <p0>v0</p0>
            // </PropertyGroup>
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup Condition=""'$(p0)'=='v0' and '@(i0)'=='a1;a2;a3;a4' and '@(i0->'%(identity).x','|')'=='a1.x|a2.x|a3.x|a4.x'"">
                  <i1 Include='x'/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookupWithItemsAndProperties(task.Project);

            task.ExecuteTask(lookup);

            ICollection<ProjectItemInstance> i1Group = lookup.GetItems("i1");

            Assert.Single(i1Group);
            Assert.Equal("x", i1Group.First().EvaluatedInclude);
        }

        /// <summary>
        /// This bug was caused by batching over the ItemGroup as well as over each child.
        /// If the condition on a child did not exclude it, an unwitting child could be included multiple times,
        /// once for each outer batch. The fix was to abandon the idea of outer batching and just
        /// prohibit batchable expressions on the ItemGroup conditions. It's just too hard to write such expressions
        /// in a comprehensible way.
        /// </summary>
        [Fact]
        public void RegressPCHBug()
        {
            // <ItemGroup>
            //    <i0 Include='a1'>
            //        <m>m1</m>
            //    </i0>
            //    <i0 Include='a2;a3'>
            //        <m>m2</m>
            //    </i0>
            //    <i0 Include='a4'>
            //        <m>m3</m>
            //    </i0>
            // </ItemGroup>
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                  <!-- squint and pretend i0 is 'CppCompile' and 'm' is 'ObjectFile' -->
                  <Link Include=""A_PCH""/>
                  <Link Include=""@(i0->'%(m).obj')"" Condition=""'%(i0.m)' == 'm1'""/>
                  <Link Include=""@(i0->'%(m)')"" Condition=""'%(i0.m)' == 'm2'""/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookup(task.Project);

            task.ExecuteTask(lookup);

            ICollection<ProjectItemInstance> linkGroup = lookup.GetItems("link");

            Assert.Equal(4, linkGroup.Count);
            Assert.Equal("A_PCH", linkGroup.First().EvaluatedInclude);
            Assert.Equal("m1.obj", linkGroup.ElementAt(1).EvaluatedInclude);
            Assert.Equal("m2", linkGroup.ElementAt(2).EvaluatedInclude);
            Assert.Equal("m2", linkGroup.ElementAt(3).EvaluatedInclude);
        }

        [Fact]
        public void RemovesOfPersistedItemsAreReversed()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='a1'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0 Remove='a1'/>
                    </ItemGroup>
                    <Message Text='[@(i0)]'/>
                  </Target>
                </Project>
            "))));

            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            // The item was removed during the build
            logger.AssertLogContains("[]");
            Assert.Empty(p.ItemsToBuildWith["i0"]);
            Assert.Empty(p.ItemsToBuildWith.ItemTypes);

            p = project.CreateProjectInstance();
            // We should still have the item left
            Assert.Single(p.ItemsToBuildWith["i0"]);
            Assert.Single(p.ItemsToBuildWith.ItemTypes);
        }

        [Fact]
        public void RemovesOfPersistedItemsAreReversed1()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='a1'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0 Include='a1'/>
                      <i0 Remove='a1'/>
                    </ItemGroup>
                    <Message Text='[@(i0)]'/>
                  </Target>
                </Project>
            "))));

            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[]");
            Assert.Empty(p.ItemsToBuildWith["i0"]);
            Assert.Empty(p.ItemsToBuildWith.ItemTypes);

            p = project.CreateProjectInstance();
            // We should still have the item left
            Assert.Single(p.ItemsToBuildWith["i0"]);
            Assert.Single(p.ItemsToBuildWith.ItemTypes);
        }

        [Fact]
        public void RemovesOfPersistedItemsAreReversed2()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='a1'/>
                    <i0 Include='a2'/>
                    <i1 Include='b1'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0 Include='a1'/>
                      <i0 Remove='a1'/>
                      <i0 Include='a1'/>
                      <i0 Include='a3'/>
                    </ItemGroup>
                    <Message Text='[@(i0)][@(i1)]'/>
                  </Target>
                </Project>
            "))));

            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[a2;a1;a3][b1]");
            Assert.Equal(3, p.ItemsToBuildWith["i0"].Count);
            Assert.Single(p.ItemsToBuildWith["i1"]);
            Assert.Equal(2, p.ItemsToBuildWith.ItemTypes.Count);

            p = project.CreateProjectInstance();
            Assert.Equal(2, p.ItemsToBuildWith["i0"].Count);
            Assert.Single(p.ItemsToBuildWith["i1"]);
            Assert.Equal(2, p.ItemsToBuildWith.ItemTypes.Count);
        }

        [Fact]
        public void RemovesOfPersistedItemsAreReversed3()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='a1'>
                      <m>m1</m>
                    </i0> 
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0 Include='a1'>
                        <m>m2</m>
                      </i0> 
                      <i0 Remove='a1'/>
                    </ItemGroup>
                    <Message Text='[%(i0.m)]'/>
                  </Target>
                </Project>
            "))));
            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[]");
            Assert.Empty(p.ItemsToBuildWith["i0"]);
            Assert.Empty(p.ItemsToBuildWith.ItemTypes);

            p = project.CreateProjectInstance();
            Assert.Single(p.ItemsToBuildWith["i0"]);
            Assert.Equal("m1", p.ItemsToBuildWith["i0"].First().GetMetadataValue("m"));
            Assert.Single(p.ItemsToBuildWith.ItemTypes);
        }

        /// <summary>
        /// Persisted item is copied into another item list by an ItemGroup -- the copy
        /// should be reversed
        /// </summary>
        [Fact]
        public void RemovesOfPersistedItemsAreReversed4()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='a1'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0 Include='@(i0)'/>
                      <i1 Include='@(i0)'/> <!-- for good measure, into another list as well -->
                    </ItemGroup>
                    <Message Text='[@(i0)][@(i1)]'/>
                  </Target>
                </Project>
            "))));

            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[a1;a1][a1;a1]");
            Assert.Equal(2, p.ItemsToBuildWith["i0"].Count);
            Assert.Equal(2, p.ItemsToBuildWith["i1"].Count);
            Assert.Equal(2, p.ItemsToBuildWith.ItemTypes.Count);

            p = project.CreateProjectInstance();
            Assert.Single(p.ItemsToBuildWith["i0"]);
            Assert.Equal("a1", p.ItemsToBuildWith["i0"].First().EvaluatedInclude);
            Assert.Empty(p.ItemsToBuildWith["i1"]);
            Assert.Single(p.ItemsToBuildWith.ItemTypes);
        }

        [Fact]
        public void RemovesOfItemsOnlyWithMetadataValue()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='a1'>
                      <m>m1</m>
                    </i0> 
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0 Include='a1'>
                        <m>m2</m>
                      </i0> 
                      <i0 Remove='a1' Condition=""'%(i0.m)' == 'm1'""/>
                    </ItemGroup>
                    <Message Text='[%(i0.m)]'/>
                  </Target>
                </Project>
            "))));
            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[m2]");
            Assert.Single(p.ItemsToBuildWith["i0"]);
        }

        [Fact]
        public void RemoveBatchingOnRemoveValue()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='m1;m2;m3'/>
                    <i1 Include='a1'>
                      <m>m1</m>
                    </i1>
                    <i1 Include='a2'>
                      <m>m2</m>
                    </i1>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0 Remove='%(i1.m)'/>
                    </ItemGroup>
                    <Message Text='[@(i0)]'/>
                  </Target>
                </Project>
            "))));
            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[m3]");
            Assert.Single(p.ItemsToBuildWith["i0"]);
        }

        [Fact]
        public void RemoveWithWildcards()
        {
            using (var env = TestEnvironment.Create())
            {
                var projectDirectory = env.CreateFolder();
                env.SetCurrentDirectory(projectDirectory.Path);

                var file1 = env.CreateFile(projectDirectory).Path;
                var file2 = env.CreateFile(projectDirectory).Path;

                string content = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                <Target Name='t'>
                    <ItemGroup>
                        <i1 Include='" + file1 + ";" + file2 + @";other'/>
                        <i1 Remove='" + projectDirectory.Path + Path.DirectorySeparatorChar + @"*.tmp'/>
                    </ItemGroup>
                </Target></Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
                Lookup lookup = LookupHelpers.CreateLookup(properties);
                ExecuteTask(task, lookup);

                Assert.Single(lookup.GetItems("i1"));
                Assert.Equal("other", lookup.GetItems("i1").First().EvaluatedInclude);
            }
        }

        [Fact]
        public void RemovesNotVisibleToParallelTargetBatches()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='1.in'><output>1.out</output></i>
                    <i Include='2.in'><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.output)'>
                    <Message Text='start:[@(i)]'/>
                    <ItemGroup>
                      <i Remove='1.in;2.in'/>
                    </ItemGroup>
                    <Message Text='end:[@(i)]'/>                    
                </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "start:[1.in]", "end:[]", "start:[2.in]", "end:[]" });
        }

        [Fact]
        public void RemovesNotVisibleToParallelTargetBatches2()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='1.in'><output>1.out</output></i>
                    <i Include='2.in'><output>2.out</output></i>
                    <j Include='j1'/>
                  </ItemGroup> 
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.output)'>
                    <Message Text='start:[@(j)]'/>
                    <ItemGroup>
                      <j Remove='@(j)'/>
                    </ItemGroup>
                    <Message Text='end:[@(j)]'/>                    
                </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "start:[j1]", "end:[]", "start:[j1]", "end:[]" });
        }

        /// <summary>
        /// Whidbey behavior was that items/properties emitted by a target being called, were
        /// not visible to subsequent tasks in the calling target. (That was because the project
        /// items and properties had been cloned for the target batches.) We must match that behavior.
        /// </summary>
        [Fact]
        public void CalledTargetItemsAreNotVisibleToCallerTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='a'/>
                  </ItemGroup>
                  <PropertyGroup>
                    <p>a</p>
                  </PropertyGroup>
                  <Target Name='t3' DependsOnTargets='t' >
                    <Message Text='after target:[$(p)][@(i)]'/>
                  </Target>
                  <Target Name='t' >
                    <CallTarget Targets='t2'/>
                    <Message Text='in target:[$(p)][@(i)]'/>
                  </Target>
                  <Target Name='t2' >
                    <CreateItem Include='b'>
                      <Output TaskParameter='include' ItemName='i'/>
                      <Output TaskParameter='include' PropertyName='q'/>
                    </CreateItem>
                    <ItemGroup>
                      <i Include='c'/>
                    </ItemGroup>
                    <PropertyGroup>
                      <p>$(p);$(q);c</p>
                    </PropertyGroup>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t3" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "in target:[a][a]", "after target:[a;b;c][a;b;c]" });
        }

        /// <summary>
        /// Items and properties should be visible within a CallTarget, even if the CallTargets are separate tasks
        /// </summary>
        [Fact]
        public void CalledTargetItemsAreVisibleWhenTargetsRunFromSeperateTasks()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Build' DependsOnTargets='t'>
        <Message Text='Props During Build:[$(SomeProperty)]'/>
        <Message Text='Items During Build:[@(SomeItem)]'/>
    </Target>

    <Target Name='t'>
        <CallTarget Targets='t1'/>
        <CallTarget Targets='t2'/>
        <Message Text='Props After t1;t2:[$(SomeProperty)]'/>
        <Message Text='Items After t1;t2:[@(SomeItem)]'/>
    </Target>
    <Target Name='t1'>
        <PropertyGroup>
            <SomeProperty>prop</SomeProperty>
        </PropertyGroup>
        <ItemGroup>
            <SomeItem Include='item'/>
        </ItemGroup>
        <Message Text='Props During t1:[$(SomeProperty)]'/>
        <Message Text='Items During t1:[@(SomeItem)]'/>
    </Target>

    <Target Name='t2'>
        <Message Text='Props During t2:[$(SomeProperty)]'/>
        <Message Text='Items During t2:[@(SomeItem)]'/>
    </Target>
</Project>
            "))));
            p.Build(new string[] { "Build" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "Props During t1:[prop]", "Props During t2:[prop]", "Props After t1;t2:[]", "Props During Build:[prop]" });
            logger.AssertLogContains(new string[] { "Items During t1:[item]", "Items During t2:[item]", "Items After t1;t2:[]", "Items During Build:[item]" });
        }

        /// <summary>
        /// Items and properties should be visible within a CallTarget, even if the targets
        /// are Run Separately
        /// </summary>
        [Fact]
        public void CalledTargetItemsAreVisibleWhenTargetsRunSeperately()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Build' DependsOnTargets='t'>
        <Message Text='Props During Build:[$(SomeProperty)]'/>
        <Message Text='Items During Build:[@(SomeItem)]'/>
    </Target>

    <Target Name='t'>
        <CallTarget Targets='t1;t2' RunEachTargetSeparately='true'/>
        <Message Text='Props After t1;t2:[$(SomeProperty)]'/>
        <Message Text='Items After t1;t2:[@(SomeItem)]'/>
    </Target>
    <Target Name='t1'>
        <PropertyGroup>
            <SomeProperty>prop</SomeProperty>
        </PropertyGroup>
        <ItemGroup>
            <SomeItem Include='item'/>
        </ItemGroup>
        <Message Text='Props During t1:[$(SomeProperty)]'/>
        <Message Text='Items During t1:[@(SomeItem)]'/>
    </Target>

    <Target Name='t2'>
        <Message Text='Props During t2:[$(SomeProperty)]'/>
        <Message Text='Items During t2:[@(SomeItem)]'/>
    </Target>
</Project>
            "))));
            p.Build(new string[] { "Build" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "Props During t1:[prop]", "Props During t2:[prop]", "Props After t1;t2:[]", "Props During Build:[prop]" });
            logger.AssertLogContains(new string[] { "Items During t1:[item]", "Items During t2:[item]", "Items After t1;t2:[]", "Items During Build:[item]" });
        }

        /// <summary>
        /// Items and properties should be visible within a CallTarget, even if the targets
        /// are Run Together
        /// </summary>
        [Fact]
        public void CalledTargetItemsAreVisibleWhenTargetsRunTogether()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <Target Name='Build' DependsOnTargets='t'>
        <Message Text='Props During Build:[$(SomeProperty)]'/>
        <Message Text='Items During Build:[@(SomeItem)]'/>
    </Target>

    <Target Name='t'>
        <CallTarget Targets='t1;t2' RunEachTargetSeparately='false'/>
        <Message Text='Props After t1;t2:[$(SomeProperty)]'/>
        <Message Text='Items After t1;t2:[@(SomeItem)]'/>
    </Target>
    <Target Name='t1'>
        <PropertyGroup>
            <SomeProperty>prop</SomeProperty>
        </PropertyGroup>
        <ItemGroup>
            <SomeItem Include='item'/>
        </ItemGroup>
        <Message Text='Props During t1:[$(SomeProperty)]'/>
        <Message Text='Items During t1:[@(SomeItem)]'/>
    </Target>

    <Target Name='t2'>
        <Message Text='Props During t2:[$(SomeProperty)]'/>
        <Message Text='Items During t2:[@(SomeItem)]'/>
    </Target>
</Project>
            "))));
            p.Build(new string[] { "Build" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "Props During t1:[prop]", "Props During t2:[prop]", "Props After t1;t2:[]", "Props During Build:[prop]" });
            logger.AssertLogContains(new string[] { "Items During t1:[item]", "Items During t2:[item]", "Items After t1;t2:[]", "Items During Build:[item]" });
        }

        /// <summary>
        /// Whidbey behavior was that items/properties emitted by a target calling another target, were
        /// not visible to the calling target. (That was because the project items and properties had been cloned for the target batches.)
        /// We must match that behavior. (For now)
        /// </summary>
        [Fact]
        public void CallerTargetItemsAreNotVisibleToCalledTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='a'/>
                  </ItemGroup>
                  <PropertyGroup>
                    <p>a</p>
                  </PropertyGroup>
                  <Target Name='t3' DependsOnTargets='t' >
                    <Message Text='after target:[$(p)][@(i)]'/>
                  </Target>
                  <Target Name='t' >
                    <CreateItem Include='b'>
                      <Output TaskParameter='include' ItemName='i'/>
                      <Output TaskParameter='include' PropertyName='q'/>
                    </CreateItem>
                    <ItemGroup>
                      <i Include='c'/>
                    </ItemGroup>
                    <PropertyGroup>
                      <p>$(p);$(q);c</p>
                    </PropertyGroup>
                    <CallTarget Targets='t2'/>
                  </Target>
                  <Target Name='t2' >
                    <Message Text='in target:[$(p)][@(i)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t3" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "in target:[a][a]", "after target:[a;b;c][a;b;c]" });
        }

        [Fact]
        public void ModifyNoOp()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup> 
                    <i1/>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assert.Empty(lookup.GetItems("i1"));
        }

        [Fact]
        public void ModifyItemInTarget()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'> 
                      <m>m1</m>
                    </i1>
                    <i1>
                      <m>m2</m>
                    </i1>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ProjectItemInstance item = lookup.GetItems("i1").First();
            Assert.Equal("m2", item.GetMetadataValue("m"));
        }

        [Fact]
        public void ModifyItemInTargetComplex()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
              <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                <PropertyGroup>
                  <p1>true</p1>
                  <p2>v3</p2>
                </PropertyGroup>

                <ItemGroup>
                   <i Include='item1'/>
                </ItemGroup>

                <Target Name='t'>
                    <ItemGroup>
                      <i>
                        <m1 Condition=""'$(p1)' == 'true'"">v1</m1>
                        <m2>v2</m2>
                        <m3>$(p2)</m3>
                      </i>
                    </ItemGroup>
                    <Message Text='[%(i.identity)|%(i.m1)|%(i.m2)|%(i.m3)]'/>
                </Target>
              </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains(@"[item1|v1|v2|v3]");
        }

        [Fact]
        public void ModifyItemInTargetLastMetadataWins()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'> 
                      <m>m1</m>
                    </i1>
                    <i1>
                      <m>m2</m>
                      <m>m3</m>
                      <m Condition='false'>m4</m>
                    </i1>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ProjectItemInstance item = lookup.GetItems("i1").First();
            Assert.Equal("m3", item.GetMetadataValue("m"));
        }

        [Fact]
        public void ModifyItemEmittedByTask()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t'>
                    <CreateItem Include='a1' AdditionalMetadata='m=m1;n=n1'>
                      <Output TaskParameter='include' ItemName='i1'/>
                    </CreateItem>
                    <ItemGroup>
                      <i1>
                        <m>m2</m>
                      </i1>
                    </ItemGroup>
                    <Message Text='[%(i1.m)][%(i1.n)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "[m2][n1]" });
        }

        [Fact]
        public void ModifyItemInTargetWithCondition()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'> 
                      <m>m1</m>
                    </i1>
                    <i1 Include='a2'> 
                      <m>m2</m>
                    </i1>
                    <i1 Condition=""'%(i1.m)'=='m2'"">
                      <m>m3</m>
                    </i1>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ProjectItemInstance item1 = lookup.GetItems("i1").First();
            ProjectItemInstance item2 = lookup.GetItems("i1").ElementAt(1);
            Assert.Equal("a1", item1.EvaluatedInclude);
            Assert.Equal("a2", item2.EvaluatedInclude);
            Assert.Equal("m1", item1.GetMetadataValue("m"));
            Assert.Equal("m3", item2.GetMetadataValue("m"));
        }

        [Fact]
        public void ModifyItemInTargetWithConditionOnMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'> 
                      <m>m1</m>
                    </i1>
                    <i1 Include='a2'> 
                      <m>m2</m>
                    </i1>
                    <i1>
                      <m Condition=""'%(i1.m)'=='m2'"">m3</m>
                    </i1>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ProjectItemInstance item1 = lookup.GetItems("i1").First();
            ProjectItemInstance item2 = lookup.GetItems("i1").ElementAt(1);
            Assert.Equal("a1", item1.EvaluatedInclude);
            Assert.Equal("a2", item2.EvaluatedInclude);
            Assert.Equal("m1", item1.GetMetadataValue("m"));
            Assert.Equal("m3", item2.GetMetadataValue("m"));
        }

        [Fact]
        public void ModifyItemWithUnqualifiedMetadataError()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'/>
                    <i1>
                      <m Condition=""'%(undefined_on_a1)'=='1'"">2</m>
                    </i1>
                </ItemGroup>
            </Target></Project>");
                IntrinsicTask task = CreateIntrinsicTask(content);
                ExecuteTask(task, null);
            }
           );
        }
        [Fact]
        public void ModifyItemInTargetWithConditionWithoutItemTypeOnMetadataInCondition()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'> 
                      <m>m1</m>
                    </i1>
                    <i1 Include='a2'> 
                      <m>m2</m>
                    </i1>
                    <i1 Condition=""'%(m)'=='m2'"">
                      <m>m3</m>
                    </i1>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ProjectItemInstance item1 = lookup.GetItems("i1").First();
            ProjectItemInstance item2 = lookup.GetItems("i1").ElementAt(1);
            Assert.Equal("a1", item1.EvaluatedInclude);
            Assert.Equal("a2", item2.EvaluatedInclude);
            Assert.Equal("m1", item1.GetMetadataValue("m"));
            Assert.Equal("m3", item2.GetMetadataValue("m"));
        }


        [Fact]
        public void ModifyItemInTargetWithConditionOnMetadataWithoutItemTypeOnMetadataInCondition()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
            @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i1 Include='a1'> 
                      <m>m1</m>
                    </i1>
                    <i1 Include='a2'> 
                      <m>m2</m>
                    </i1>
                    <i1>
                      <m Condition=""'%(m)'=='m2'"">m3</m>
                    </i1>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            ProjectItemInstance item1 = lookup.GetItems("i1").First();
            ProjectItemInstance item2 = lookup.GetItems("i1").ElementAt(1);
            Assert.Equal("a1", item1.EvaluatedInclude);
            Assert.Equal("a2", item2.EvaluatedInclude);
            Assert.Equal("m1", item1.GetMetadataValue("m"));
            Assert.Equal("m3", item2.GetMetadataValue("m"));
        }

        [Fact]
        public void ModifyItemOutsideTarget()
        {
            // <ItemGroup>
            //    <i0 Include='a1'>
            //        <m>m1</m>
            //        <n>n1</n>
            //    </i0>
            //    <i0 Include='a2;a3'>
            //        <m>m2</m>
            //        <n>n2</n>
            //    </i0>
            //    <i0 Include='a4'>
            //        <m>m3</m>
            //        <n>n3</n>
            //    </i0>
            // </ItemGroup>
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
            <Target Name='t'>
                <ItemGroup>
                    <i0>
                      <m>m4</m>
                    </i0>
                </ItemGroup>
            </Target></Project>");
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookup(task.Project);

            task.ExecuteTask(lookup);

            ICollection<ProjectItemInstance> i0Group = lookup.GetItems("i0");

            Assert.Equal(4, i0Group.Count);
            foreach (ProjectItemInstance item in i0Group)
            {
                Assert.Equal("m4", item.GetMetadataValue("m"));
            }
        }

        [Fact]
        public void RemoveComplexMidlExample()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
  <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
    <PropertyGroup>
      <UseIdlBasedDllData>true</UseIdlBasedDllData>
      <MidlDllDataFileName>dlldata.c</MidlDllDataFileName>
      <MidlDllDataDir>dlldatadir</MidlDllDataDir>
      <MidlHeaderDir>headerdir</MidlHeaderDir>
      <MidlTlbDir>tlbdir</MidlTlbDir>
      <MidlProxyDir>proxydir</MidlProxyDir>
      <MidlInterfaceDir>interfacedir</MidlInterfaceDir>
    </PropertyGroup>

    <ItemGroup>
       <Idl Include='a.idl'/>
       <Idl Include='b.idl'>
          <DllDataFileName>mydlldata.c</DllDataFileName>
       </Idl>
       <Idl Include='c.idl'>
          <HeaderFileName>myheader.h</HeaderFileName>
       </Idl>
    </ItemGroup>

    <Target Name='MIDL'>
        <Message Text='Before: [%(idl.identity)|%(Idl.m1)]'/>
        <ItemGroup>
          <Idl>
            <DllDataFileName Condition=""'$(UseIdlBasedDllData)' == 'true' and '%(Idl.DllDataFileName)' == ''"">$(MidlDllDataDir)\%(Filename)_dlldata.c</DllDataFileName>
            <DllDataFileName Condition=""'$(UseIdlBasedDllData)' != 'true' and '%(Idl.DllDataFileName)' == ''"">$(MidlDllDataFileName)</DllDataFileName>
            <HeaderFileName Condition=""'%(Idl.HeaderFileName)' == ''"">$(MidlHeaderDir)\%(Idl.Filename).h</HeaderFileName>
            <TypeLibraryName Condition=""'%(Idl.TypeLibraryName)' == ''"">$(MidlTlbDir)\%(Filename).tlb</TypeLibraryName>
            <ProxyFileName Condition=""'%(Idl.ProxyFileName)' == ''"">$(MidlProxyDir)\%(Filename)_p.c</ProxyFileName>
            <InterfaceIdentifierFileName Condition=""'%(Idl.InterfaceIdentifierFileName)' == ''"">$(MidlInterfaceDir)\%(Filename)_i.c</InterfaceIdentifierFileName>
          </Idl>
        </ItemGroup>

        <Message Text='[%(idl.identity)|%(idl.dlldatafilename)|%(idl.headerfilename)|%(idl.TypeLibraryName)|%(idl.ProxyFileName)|%(idl.InterfaceIdentifierFileName)]'/>
    </Target>
  </Project>
            "))));
            p.Build(new string[] { "MIDL" }, new ILogger[] { logger });

            logger.AssertLogContains(@"[a.idl|dlldatadir\a_dlldata.c|headerdir\a.h|tlbdir\a.tlb|proxydir\a_p.c|interfacedir\a_i.c]",
                                     @"[b.idl|mydlldata.c|headerdir\b.h|tlbdir\b.tlb|proxydir\b_p.c|interfacedir\b_i.c]",
                                     @"[c.idl|dlldatadir\c_dlldata.c|myheader.h|tlbdir\c.tlb|proxydir\c_p.c|interfacedir\c_i.c]");
        }

        [Fact]
        public void ModifiesOfPersistedItemsAreReversed1()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='i1'>
                      <m>m0</m>
                    </i0>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i0>
                        <m>m1</m>
                      </i0> 
                    </ItemGroup>
                  </Target>
                  <Target Name='t2'>
                    <Message Text='[%(i0.m)]'/>
                  </Target>
                </Project>
            "))));

            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t", "t2" }, new ILogger[] { logger });

            logger.AssertLogContains("[m1]");

            ProjectItemInstance item = p.ItemsToBuildWith["i0"].First();
            Assert.Equal("m1", item.GetMetadataValue("m"));

            p = project.CreateProjectInstance();
            item = p.ItemsToBuildWith["i0"].First();
            Assert.Equal("m0", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Modify of an item copied during the build
        /// </summary>
        [Fact]
        public void ModifiesOfPersistedItemsAreReversed2()
        {
            MockLogger logger = new MockLogger();
            Project project = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i0 Include='i1'>
                      <m>m0</m>
                      <n>n0</n>
                    </i0>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <i1 Include='@(i0)'>
                        <m>m1</m>
                      </i1>
                      <i1>
                        <n>n1</n>
                      </i1> 
                    </ItemGroup>
                  </Target>
                  <Target Name='t2'>
                    <Message Text='[%(i0.m)][%(i0.n)]'/>
                    <Message Text='[%(i1.m)][%(i1.n)]'/>
                  </Target>
                </Project>
            "))));

            ProjectInstance p = project.CreateProjectInstance();
            p.Build(new string[] { "t", "t2" }, new ILogger[] { logger });

            logger.AssertLogContains("[m0][n0]", "[m1][n1]");

            Assert.Single(p.ItemsToBuildWith["i0"]);
            Assert.Single(p.ItemsToBuildWith["i1"]);
            Assert.Equal("m0", p.ItemsToBuildWith["i0"].First().GetMetadataValue("m"));
            Assert.Equal("n0", p.ItemsToBuildWith["i0"].First().GetMetadataValue("n"));
            Assert.Equal("m1", p.ItemsToBuildWith["i1"].First().GetMetadataValue("m"));
            Assert.Equal("n1", p.ItemsToBuildWith["i1"].First().GetMetadataValue("n"));

            p = project.CreateProjectInstance();
            Assert.Single(p.ItemsToBuildWith["i0"]);
            Assert.Empty(p.ItemsToBuildWith["i1"]);
            Assert.Equal("m0", p.ItemsToBuildWith["i0"].First().GetMetadataValue("m"));
            Assert.Equal("n0", p.ItemsToBuildWith["i0"].First().GetMetadataValue("n"));
        }


        /// <summary>
        /// The case is where a transform is done on an item to generate a pdb file name when the extension of an item is dll
        /// the resulting items is expected to have an extension metadata of pdb but instead has an extension of dll
        /// </summary>
        [Fact]
        public void IncludeCheckOnMetadata()
        {
            MockLogger logger = new MockLogger();

            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                   <Target Name='a'>
                     <ItemGroup>
                       <Content Include='a.dll' />
                       <Content Include=""@(Content->'%(FileName).pdb')"" Condition=""'%(Content.Extension)' == '.dll'""/>
                      </ItemGroup>

                      <Message Text='[%(Content.Identity)]->[%(Content.Extension)]' Importance='High'/>
                   </Target>
                </Project> "))));
            bool success = p.Build(new string[] { "a" }, new ILogger[] { logger });
            Assert.True(success);
            logger.AssertLogContains("[a.dll]->[.dll]");
            logger.AssertLogContains("[a.pdb]->[.pdb]");
        }

        /// <summary>
        /// The case is where a transform is done on an item to generate a pdb file name the batching is done on the identity.
        /// If the identity was also copied over then we would only get one bucket instead of two buckets
        /// </summary>
        [Fact]
        public void IncludeCheckOnMetadata2()
        {
            MockLogger logger = new MockLogger();

            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                   <Target Name='a'>
                     <ItemGroup>
                       <Content Include='a.dll' />
                       <Content Include=""@(Content->'%(FileName)%(Extension).pdb')""/>
                       <Content Include=""@(Content->'%(FileName)%(Extension).pdb')"" Condition=""'%(Content.Identity)' != ''""/>
                      </ItemGroup>

                      <Message Text='[%(Content.Identity)]->[%(Content.Extension)]' Importance='High'/>
                   </Target>
                </Project> "))));
            bool success = p.Build(new string[] { "a" }, new ILogger[] { logger });
            Assert.True(success);
            logger.AssertLogContains("[a.dll]->[.dll]");
            logger.AssertLogContains("[a.dll.pdb]->[.pdb]");
            logger.AssertLogContains("[a.dll.pdb.pdb]->[.pdb]");
        }

        /// <summary>
        /// Make sure that recursive dir still gets the right file
        ///
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        [Trait("Category", "mono-osx-failing")]
        public void IncludeCheckOnMetadata_3()
        {
            MockLogger logger = new MockLogger();

            string tempPath = Path.GetTempPath();
            string directoryForTest = Path.Combine(tempPath, "IncludeCheckOnMetadata_3\\Test");
            string fileForTest = Path.Combine(directoryForTest, "a.dll");

            try
            {
                if (Directory.Exists(directoryForTest))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(directoryForTest, true);
                }
                else
                {
                    Directory.CreateDirectory(directoryForTest);
                }

                File.WriteAllText(fileForTest, fileForTest);

                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                   <Target Name='a'>
                     <ItemGroup>
                        <Content Include='a.dll' />
                        <Content Include='" + Path.Combine(directoryForTest, "..", "**") + @"'  Condition=""'%(Content.Extension)' == '.dll'""/>
                    </ItemGroup>
                         <Message Text='[%(Content.Identity)]->[%(Content.Extension)]->[%(Content.RecursiveDir)]' Importance='High'/>
                     </Target>
                </Project> "))));
                bool success = p.Build(new string[] { "a" }, new ILogger[] { logger });
                Assert.True(success);
                logger.AssertLogContains("[a.dll]->[.dll]->[]");
                logger.AssertLogContains(
                    "[" + Path.Combine(directoryForTest, "..", "Test", "a.dll") + @"]->[.dll]->[Test"
                    + Path.DirectorySeparatorChar + "]");
            }
            finally
            {
                if (Directory.Exists(directoryForTest))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(directoryForTest, true);
                }
            }
        }

        [Fact]
        public void RemoveItemInImportedFile()
        {
            MockLogger logger = new MockLogger();
            string importedFile = null;

            try
            {
                importedFile = FileUtilities.GetTemporaryFile();
                File.WriteAllText(importedFile, ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i1 Include='imported'/>
                  </ItemGroup>
                </Project>
            "));
                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                      <Import Project='" + importedFile + @"'/>
                      <Target Name='t'>
                        <Message Text='[@(i1)]'/>
                        <ItemGroup>
                          <i1 Remove='imported'/>
                        </ItemGroup>
                        <Message Text='[@(i1)]'/>
                      </Target>
                    </Project>
                "))));
                p.Build(new string[] { "t" }, new ILogger[] { logger });

                logger.AssertLogContains("[imported]", "[]");
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(new string[] { importedFile });
            }
        }

        [Fact]
        public void ModifyItemInImportedFile()
        {
            MockLogger logger = new MockLogger();
            string importedFile = null;

            try
            {
                importedFile = FileUtilities.GetTemporaryFile();
                File.WriteAllText(importedFile, ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i1 Include='imported'/>
                  </ItemGroup>
                </Project>
            "));
                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                      <Import Project='" + importedFile + @"'/>
                      <Target Name='t'>
                        <ItemGroup>
                          <i1>
                            <m>m1</m>
                          </i1>
                        </ItemGroup>
                        <Message Text='[%(i1.m)]'/>
                      </Target>
                    </Project>
                "))));
                p.Build(new string[] { "t" }, new ILogger[] { logger });

                logger.AssertLogContains("[m1]");
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(new string[] { importedFile });
            }
        }

        /// <summary>
        /// Properties produced in one target batch are not visible to another
        /// </summary>
        [Fact]
        public void OutputPropertiesInTargetBatchesCreateItem()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <!-- just to cause two target batches -->
                    <i Include='1.in'><output>1.out</output></i>
                    <i Include='2.in'><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.output)'>
                    <Message Text='start:[$(p)]'/>
                    <CreateProperty Value='$(p)--%(i.Identity)'>
                      <Output TaskParameter='Value' PropertyName='p'/>
                    </CreateProperty>
                    <Message Text='end:[$(p)]'/>
                  </Target>
                  <Target Name='t2'>
                    <Message Text='final:[$(p)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t", "t2" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "start:[]", "end:[--1.in]", "start:[]", "end:[--2.in]", "final:[--2.in]" });
        }

        /// <summary>
        /// Properties produced in one task batch are not visible to another
        /// </summary>
        [Fact]
        public void OutputPropertiesInTaskBatchesCreateItem()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <Target Name='t'>
                    <ItemGroup>
                      <i Include='1.in;2.in'/>
                    </ItemGroup>
                    <CreateProperty Value='$(p)--%(i.Identity)'>
                      <Output TaskParameter='Value' PropertyName='p'/>
                    </CreateProperty>
                    <Message Text='end:[$(p)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains(new string[] { "end:[--2.in]" });
        }

        /// <summary>
        /// In this case gen.cpp was getting ObjectFile of def.obj.
        /// </summary>
        [Fact]
        public void PhoenixBatchingIssue()
        {
            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                <ItemGroup>
                    <CppCompile Include='gen.cpp'/>
                    <CppCompile Include='def.cpp'>
                        <ObjectFile>def.obj</ObjectFile>
                    </CppCompile>
                </ItemGroup>
                
                <Target Name='t'>
                    <ItemGroup>
                        <CppCompile>
                            <IncludeInLib Condition=""'%(CppCompile.IncludeInLib)' == ''"">true</IncludeInLib>
                        </CppCompile>
                        <CppCompile>
                            <ObjectFile>%(Filename).obj</ObjectFile>
                        </CppCompile>
                    </ItemGroup>
                </Target>
            </Project>
            "))));
            ProjectInstance instance = new ProjectInstance(xml);
            instance.Build();

            Assert.Equal(2, instance.Items.Count());
            Assert.Equal("gen.obj", instance.GetItems("CppCompile").First().GetMetadataValue("ObjectFile"));
            Assert.Equal("def.obj", instance.GetItems("CppCompile").Last().GetMetadataValue("ObjectFile"));
        }

        [Fact]
        public void PropertiesInInferredBuildCreateProperty()
        {
            string[] files = null;
            try
            {
                files = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));

                MockLogger logger = new MockLogger();
                Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='" + files.First() + "'><output>" + files.ElementAt(1) + @"</output></i>
                  </ItemGroup>
                  <Target Name='t2' DependsOnTargets='t'>
                    <Message Text='final:[$(p)]'/>
                  </Target>
                  <Target Name='t' Inputs='%(i.Identity)' Outputs='%(i.Output)'>
                    <Message Text='start:[$(p)]'/>
                    <CreateProperty Value='@(i)'>
                      <Output TaskParameter='Value' PropertyName='p'/>
                    </CreateProperty>
                    <Message Text='end:[$(p)]'/>
                </Target>
                </Project>
            "))));
                p.Build(new string[] { "t2" }, new ILogger[] { logger });

                // We should only see messages from the second target, as the first is only inferred
                logger.AssertLogDoesntContain("start:");
                logger.AssertLogDoesntContain("end:");
                logger.AssertLogContains(new string[] { "final:[" + files.First() + "]" });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(files);
            }
        }

        [Fact]
        public void ModifyItemPreviouslyModified()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <x Include='a'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x>
                        <m1>2</m1>
                      </x>  
                    </ItemGroup>
                    <Message Text='[%(x.m1)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Fact]
        public void ModifyItemPreviouslyModified2()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <x Include='a'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                    </ItemGroup>
                    <ItemGroup>
                      <x>
                        <m1>2</m1>
                      </x>  
                    </ItemGroup>
                    <Message Text='[%(x.m1)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Fact]
        public void RemoveItemPreviouslyModified()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <x Include='a'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x Remove='@(x)'/>
                    </ItemGroup>
                    <Message Text='[%(x.m1)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogDoesntContain("[2]");
        }

        [Fact]
        public void RemoveItemPreviouslyModified2()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <x Include='a'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                    </ItemGroup>
                    <ItemGroup>
                      <x Remove='@(x)'/>
                    </ItemGroup>
                    <Message Text='[%(x.m1)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogDoesntContain("[2]");
        }

        [Fact]
        public void FilterItemPreviouslyModified()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <x Include='a'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x Condition=""'%(x.m1)'=='1'"">
                        <m1>2</m1>
                      </x>
                    </ItemGroup>
                    <Message Text='[%(x.m1)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Fact]
        public void FilterItemPreviouslyModified2()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <x Include='a'/>
                  </ItemGroup>
                  <Target Name='t'>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x>
                        <m1 Condition=""'%(x.m1)'=='1'"">2</m1>
                      </x>
                    </ItemGroup>
                    <Message Text='[%(x.m1)]'/>
                  </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Fact]
        public void FilterItemPreviouslyModified3()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                   <ItemGroup>
                       <A Include='a;b;c'>
                           <m>m1</m>
                       </A>
                   </ItemGroup>
                   <Target Name='t'>
                       <ItemGroup>
                           <A Condition=""'%(m)' == 'm1'"">
                               <m>m2</m>
                           </A>
                       </ItemGroup>
                       <ItemGroup>
                           <A Condition=""'%(m)' == 'm2'"">
                               <m>m3</m>
                           </A>
                       </ItemGroup>
                       <ItemGroup>
                           <A Condition=""'%(m)' == 'm3'"">
                               <m>m4</m>
                           </A>
                       </ItemGroup>
                       <Message Text='[@(A) = %(A.m)]'/>
                   </Target>
                </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[a;b;c = m4]");
        }

        [Fact]
        public void FilterItemPreviouslyModified4()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                   <Target Name='t'>
                       <ItemGroup>
                           <A Include='a;b;c'>
                               <m>m1</m>
                           </A>
                           <A Condition=""'%(Identity)' == 'a' or '%(Identity)' == 'c'"">
                               <m>m2</m>
                           </A>
                           <A Condition=""'%(Identity)' == 'a' or '%(Identity)' == 'c'"">
                               <m>m3</m>
                           </A>
                       </ItemGroup>
                       <Message Text='[@(A) = %(A.m)]'/>
                   </Target>
               </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[b = m1]");
            logger.AssertLogContains("[a;c = m3]");
        }

        [Fact]
        public void FilterItemPreviouslyModified5()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                   <Target Name='t'>
                       <ItemGroup>
                           <A Include='a;b;c'>
                               <m>m1</m>
                           </A>
                           <A Condition=""'%(Identity)' == 'a' or '%(Identity)' == 'c'"">
                               <m>m2</m>
                           </A>
                           <A Condition=""'%(Identity)' == 'a'"">
                               <m>m3</m>
                           </A>
                       </ItemGroup>
                       <Message Text='[@(A) = %(A.m)]'/>
                   </Target>
               </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[a = m3]");
            logger.AssertLogContains("[b = m1]");
            logger.AssertLogContains("[c = m2]");
        }

        [Fact]
        public void FilterItemPreviouslyModified6()
        {
            MockLogger logger = new MockLogger();
            Project p = new Project(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <A Include='a;b;c'>
                            <m>m1</m>
                        </A>
                    </ItemGroup>
                    <Target Name='t'>
                        <ItemGroup>
                            <A Condition=""'%(m)' == 'm1'"">
                                <m>m2</m>
                            </A>
                        </ItemGroup>
                        <ItemGroup>
                            <A Condition=""'%(m)' == 'm2'"">
                                <m></m>
                            </A>
                        </ItemGroup>
                        <ItemGroup>
                            <A Condition=""'%(m)' == 'm3'"">
                                <m>m3</m>
                            </A>
                        </ItemGroup>
                        <Message Text='[@(A)=%(A.m)]'/>
                    </Target>
               </Project>
            "))));
            p.Build(new string[] { "t" }, new ILogger[] { logger });

            logger.AssertLogContains("[a;b;c=]");
        }
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Helpers

        private static PropertyDictionary<ProjectPropertyInstance> GeneratePropertyGroup()
        {
            PropertyDictionary<ProjectPropertyInstance> properties = new PropertyDictionary<ProjectPropertyInstance>();
            properties.Set(ProjectPropertyInstance.Create("p0", "v0"));
            return properties;
        }

        private static Lookup GenerateLookupWithItemsAndProperties(ProjectInstance project)
        {
            PropertyDictionary<ProjectPropertyInstance> pg = new PropertyDictionary<ProjectPropertyInstance>();
            pg.Set(ProjectPropertyInstance.Create("p0", "v0"));

            Lookup lookup = GenerateLookup(project, pg);
            return lookup;
        }

        private static Lookup GenerateLookup(ProjectInstance project)
        {
            return GenerateLookup(project, new PropertyDictionary<ProjectPropertyInstance>());
        }

        private static Lookup GenerateLookup(ProjectInstance project, PropertyDictionary<ProjectPropertyInstance> properties)
        {
            List<ProjectItemInstance> items = new List<ProjectItemInstance>();
            ProjectItemInstance item1 = new ProjectItemInstance(project, "i0", "a1", project.FullPath);
            ProjectItemInstance item2 = new ProjectItemInstance(project, "i0", "a2", project.FullPath);
            ProjectItemInstance item3 = new ProjectItemInstance(project, "i0", "a3", project.FullPath);
            ProjectItemInstance item4 = new ProjectItemInstance(project, "i0", "a4", project.FullPath);
            item1.SetMetadata("m", "m1");
            item1.SetMetadata("n", "n1");
            item2.SetMetadata("m", "m2");
            item2.SetMetadata("n", "n2");
            item3.SetMetadata("m", "m2");
            item3.SetMetadata("n", "n2");
            item4.SetMetadata("m", "m3");
            item4.SetMetadata("n", "n3");
            items.Add(item1);
            items.Add(item2);
            items.Add(item3);
            items.Add(item4);
            ItemDictionary<ProjectItemInstance> itemsByName = new ItemDictionary<ProjectItemInstance>();
            itemsByName.ImportItems(items);

            Lookup lookup = LookupHelpers.CreateLookup(properties, itemsByName);

            return lookup;
        }

        private static IntrinsicTask CreateIntrinsicTask(string content)
        {
            Project project = new Project(XmlReader.Create(new StringReader(content)));
            ProjectInstance projectInstance = project.CreateProjectInstance();
            ProjectTargetInstanceChild targetChild = projectInstance.Targets["t"].Children.First();

            NodeLoggingContext nodeContext = new NodeLoggingContext(new MockLoggingService(), 1, false);
            BuildRequestEntry entry = new BuildRequestEntry(new BuildRequest(1 /* submissionId */, 0, 1, new string[] { "t" }, null, BuildEventContext.Invalid, null), new BuildRequestConfiguration(1, new BuildRequestData("projectFile", new Dictionary<string, string>(), "3.5", new string[0], null), "2.0"));
            entry.RequestConfiguration.Project = projectInstance;
            IntrinsicTask task = IntrinsicTask.InstantiateTask(
                targetChild,
                nodeContext.LogProjectStarted(entry).LogTargetBatchStarted(projectInstance.FullPath, projectInstance.Targets["t"], null, TargetBuiltReason.None),
                projectInstance,
                false);

            return task;
        }

        private void ExecuteTask(IntrinsicTask task)
        {
            ExecuteTask(task, null);
        }

        private void ExecuteTask(IntrinsicTask task, Lookup lookup)
        {
            if (lookup == null)
            {
                lookup = LookupHelpers.CreateEmptyLookup();
            }

            task.ExecuteTask(lookup);
        }

        internal static void AssertItemEvaluationFromTarget(string projectContents, string targetName, string itemType, string[] inputFiles, string[] expectedInclude, bool makeExpectedIncludeAbsolute = false, Dictionary<string, string>[] expectedMetadataPerItem = null, bool normalizeSlashes = false)
        {
            ObjectModelHelpers.AssertItemEvaluationFromGenericItemEvaluator((p, c) =>
                {
                    var project = new Project(p, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, c);
                    var projectInstance = project.CreateProjectInstance();
                    var targetChild = projectInstance.Targets["t"].Children.First();

                    var nodeContext = new NodeLoggingContext(new MockLoggingService(), 1, false);
                    var entry = new BuildRequestEntry(new BuildRequest(1 /* submissionId */, 0, 1, new string[] { targetName }, null, BuildEventContext.Invalid, null), new BuildRequestConfiguration(1, new BuildRequestData("projectFile", new Dictionary<string, string>(), "3.5", new string[0], null), "2.0"));
                    entry.RequestConfiguration.Project = projectInstance;
                    var task = IntrinsicTask.InstantiateTask(
                        targetChild,
                        nodeContext.LogProjectStarted(entry).LogTargetBatchStarted(projectInstance.FullPath, projectInstance.Targets["t"], null, TargetBuiltReason.None),
                        projectInstance,
                        false);

                    var lookup = new Lookup(new ItemDictionary<ProjectItemInstance>(), new PropertyDictionary<ProjectPropertyInstance>());
                    task.ExecuteTask(lookup);

                    return lookup.GetItems(itemType).Select(i => (ObjectModelHelpers.TestItem)new ObjectModelHelpers.ProjectItemInstanceTestItemAdapter(i)).ToList();
                },
                projectContents,
                inputFiles,
                expectedInclude,
                makeExpectedIncludeAbsolute,
                expectedMetadataPerItem,
                normalizeSlashes);
        }
        #endregion
    }
}
