// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;
using System.Xml;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class IntrinsicTask_Tests
    {
        [Test]
        public void PropertyGroup()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup> 
                    <p1>v1</p1>
                    <p2>v2</p2>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(2, properties.Count);
            Assertion.AssertEquals("v1", properties["p1"].FinalValue);
            Assertion.AssertEquals("v2", properties["p2"].FinalValue);
        }

        [Test]
        public void PropertyGroupWithComments()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup><!-- c -->
                    <p1>v1</p1><!-- c -->
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(1, properties.Count);
            Assertion.AssertEquals("v1", properties["p1"].FinalValue);
        }

        [Test]
        public void PropertyGroupEmpty()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup/>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(0, properties.Count);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void PropertyGroupWithReservedProperty()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup>
                  <MSBuildProjectFile/>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task);
        }


        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void PropertyGroupWithInvalidPropertyName()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup>
                  <PropertyGroup/>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task);
        }

        [Test]
        public void BlankProperty()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup>
                  <p1></p1>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(1, properties.Count);
            Assertion.AssertEquals("", properties["p1"].FinalValue);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void PropertyGroupWithInvalidSyntax1()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup>x</PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task, null);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void PropertyGroupWithInvalidSyntax2()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup>
                    <p Include='v0'/>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task, null);
        }

        [Test]
        public void PropertyGroupWithConditionOnGroup()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup Condition='false'> 
                    <p1>v1</p1>
                    <p2>v2</p2>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(0, properties.Count);

            content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup Condition='true'> 
                    <p1>v1</p1>
                    <p2>v2</p2>
                </PropertyGroup>
            </Target>";
            task = CreateIntrinsicTask(content);
            properties = new BuildPropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(2, properties.Count);
            Assertion.AssertEquals("v1", properties["p1"].FinalValue);
            Assertion.AssertEquals("v2", properties["p2"].FinalValue);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void PropertyGroupWithConditionOnGroupUsingMetadataErrors()
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup Condition=""'%(i0.m)'=='m2'""> 
                    <p1>@(i0)</p1>
                    <p2>%(i0.m)</p2>
                </PropertyGroup>
            </Target>";

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup();
            ExecuteTask(task, lookup);
        }

        [Test]
        public void ItemGroup()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i2 Include='b1'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");
            Assertion.AssertEquals("a1", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("b1", i2Group[0].FinalItemSpec);
        }

        [Test]
        public void ItemGroupWithComments()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> <!-- c -->
                    <i1 Include='a1;a2'/> <!-- c -->
                    <ii Remove='a1'/> <!-- c -->
                    <i1> <!-- c -->
                        <m>m1</m> <!-- c -->
                    </i1> <!-- c -->
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            Assertion.AssertEquals("a1", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("m1", i1Group[0].GetMetadata("m"));
        }

        /// <summary>
        /// This is something that used to be done by CreateItem
        /// </summary>
        [Test]
        public void ItemGroupTrims()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='  $(p0)  '/>
                    <i2 Include='b1'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            properties.SetProperty("p0", "    v0    ");
            Lookup lookup = LookupHelpers.CreateLookup(properties);
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            Assertion.AssertEquals("v0", i1Group[0].FinalItemSpec);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemGroupWithInvalidSyntax1()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>x</ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task, null);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemGroupWithInvalidSyntax2()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                  <i>x</i>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task, null);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemGroupWithInvalidSyntax3()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                  <i Include='x' Exclude='y' Remove='z'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task, null);
        }

        [Test]
        public void ItemGroupWithTransform()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='a.cpp'/>
                    <i2 Include=""@(i1->'%(filename).obj')""/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");
            Assertion.AssertEquals("a.cpp", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("a.obj", i2Group[0].FinalItemSpec);
        }

        [Test]
        public void ItemGroupWithTransformInMetadataValue()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='a.cpp'/>
                    <i2 Include='@(i1)'>
                       <m>@(i1->'%(filename).obj')</m>
                    </i2>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i2Group = lookup.GetItems("i2");
            Assertion.AssertEquals("a.cpp", i2Group[0].FinalItemSpec);
            Assertion.AssertEquals("a.obj", i2Group[0].GetEvaluatedMetadata("m"));
        }

        [Test]
        public void ItemGroupWithExclude()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='a1'/>
                    <i2 Include='a1;@(i1);b1;b2' Exclude='@(i1);b1'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");
            Assertion.AssertEquals("a1", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("b2", i2Group[0].FinalItemSpec);
        }

        [Test]
        public void ItemGroupWithMetadataInExclude()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='a1'>
                        <m>a1</m>
                    </i1>
                    <i2 Include='b1;@(i1)' Exclude='%(i1.m)'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");
            Assertion.AssertEquals(1, i1Group.Count);
            Assertion.AssertEquals(1, i2Group.Count);
            Assertion.AssertEquals("a1", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("b1", i2Group[0].FinalItemSpec);
        }

        [Test]
        public void ItemGroupWithConditionOnGroup()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup Condition='false'> 
                    <i1 Include='a1'/>
                    <i2 Include='b1'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
            Assertion.AssertEquals(0, lookup.GetItems("i2").Count);

            content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup Condition='true'> 
                    <i1 Include='a1'/>
                    <i2 Include='b1'/>
                </ItemGroup>
            </Target>";
            task = CreateIntrinsicTask(content);
            lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");
            Assertion.AssertEquals("a1", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("b1", i2Group[0].FinalItemSpec);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemGroupWithConditionOnGroupUsingMetadataErrors()
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup Condition=""'%(i0.m)'!='m1'""> 
                    <i1 Include='a1'/>
                    <i2 Include='%(i0.m)'/>
                    <i3 Include='%(i0.identity)'/>
                    <i4 Include='@(i0)'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup();
            ExecuteTask(task, lookup);
        }

        [Test]
        public void PropertyGroupWithExternalPropertyReferences()
        {
            // <PropertyGroup>
            //     <p0>v0</p0>
            // </PropertyGroup>
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup> 
                    <p1>$(p0)</p1>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = GeneratePropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(2, properties.Count);
            Assertion.AssertEquals("v0", properties["p0"].FinalValue);
            Assertion.AssertEquals("v0", properties["p1"].FinalValue);
        }

        [Test]
        public void ItemGroupWithPropertyReferences()
        {
            // <PropertyGroup>
            //     <p0>v0</p0>
            // </PropertyGroup>
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='$(p0)'/>
                    <i2 Include='a2'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = GeneratePropertyGroup();
            Lookup lookup = LookupHelpers.CreateLookup(properties);
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");
            Assertion.AssertEquals("v0", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("a2", i2Group[0].FinalItemSpec);
        }

        [Test]
        public void ItemGroupWithMetadataReferences()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='a1'>
                        <m>m1</m>
                    </i1>
                    <i1 Include='a2'>
                        <m>m2</m>
                    </i1>
                    <i2 Include='%(i1.m)'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");

            Assertion.AssertEquals("a1", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("a2", i1Group[1].FinalItemSpec);
            Assertion.AssertEquals("m1", i2Group[0].FinalItemSpec);
            Assertion.AssertEquals("m2", i2Group[1].FinalItemSpec);

            Assertion.AssertEquals("m1", i1Group[0].GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("m2", i1Group[1].GetEvaluatedMetadata("m"));
        }

        [Test]
        public void ItemGroupWithMetadataReferencesOnMetadataConditions()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i2Group = lookup.GetItems("i2");

            Assertion.AssertEquals(2, i2Group.Count);
            Assertion.AssertEquals("a1", i2Group[0].FinalItemSpec);
            Assertion.AssertEquals("a2", i2Group[1].FinalItemSpec);

            Assertion.AssertEquals("n1", i2Group[0].GetEvaluatedMetadata("n"));
            Assertion.AssertEquals("", i2Group[1].GetEvaluatedMetadata("n"));
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemGroupWithMetadataReferencesOnItemGroupAndItemConditionsErrors()
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup Condition=""'%(i0.m)' != m1"" >
                    <i1 Include=""%(m)"" Condition=""'%(i0.m)' != m3""/>
                </ItemGroup>
            </Target>";

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup();
            ExecuteTask(task, lookup);
        }

        [Test]
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include='b1'>
                        <m>%(i0.m)</m>
                    </i1>
                    <i2 Include='%(i1.m)'/>
                </ItemGroup>
            </Target>";

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup();
            ExecuteTask(task, lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");
            BuildItemGroup i2Group = lookup.GetItems("i2");

            Assertion.AssertEquals("b1", i1Group[0].FinalItemSpec);
            Assertion.AssertEquals("b1", i1Group[1].FinalItemSpec);
            Assertion.AssertEquals("b1", i1Group[2].FinalItemSpec);
            Assertion.AssertEquals("m1", i1Group[0].GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("m2", i1Group[1].GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("m3", i1Group[2].GetEvaluatedMetadata("m"));

            Assertion.AssertEquals("m1", i2Group[0].FinalItemSpec);
            Assertion.AssertEquals("m2", i2Group[1].FinalItemSpec);
            Assertion.AssertEquals("m3", i2Group[2].FinalItemSpec);
        }

        [Test]
        public void PropertyGroupWithCumulativePropertyReferences()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup> 
                    <p1>v1</p1>
                    <p2>#$(p1)#</p2>
                    <p1>v2</p1>
                </PropertyGroup>
            </Target>";

            IntrinsicTask task = CreateIntrinsicTask(content);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            ExecuteTask(task, LookupHelpers.CreateLookup(properties));

            Assertion.AssertEquals(2, properties.Count);
            Assertion.AssertEquals("v2", properties["p1"].FinalValue);
            Assertion.AssertEquals("#v1#", properties["p2"].FinalValue);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void PropertyGroupWithMetadataReferencesOnGroupErrors()
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup Condition=""'%(i0.m)' != m1""> 
                    <p1>%(i0.m)</p1>
                </PropertyGroup>
            </Target>";

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup();
            ExecuteTask(task, lookup);
        }

        [Test]
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup> 
                    <p1 Condition=""'%(i0.n)' != n3"">%(i0.n)</p1>
                </PropertyGroup>
            </Target>";

            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals("n2", lookup.GetProperty("p1").FinalValue);
        }

        [Test]
        public void PropertiesCanReferenceItemsInSameTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i1 Include=`a1;a2`/>
                    </ItemGroup>
                    <PropertyGroup>
                      <p>@(i1->'#%(identity)#', '*')</p>
                    </PropertyGroup>
                    <Message Text=`[$(p)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[#a1#*#a2#]");
        }

        [Test]
        public void ItemsCanReferencePropertiesInSameTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <PropertyGroup>
                        <p0>v0</p0>
                    </PropertyGroup>
                    <ItemGroup> 
                        <i1 Include='$(p0)'/>
                    </ItemGroup>
                    <Message Text=`[@(i1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[v0]");
        }

        [Test]
        public void PropertyGroupInTargetCanOverwriteGlobalProperties()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <global>v1</global>
                  </PropertyGroup>
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`final:[$(global)]`/>
                  </Target>
                  <Target Name=`t`>
                    <Message Text=`start:[$(global)]`/>
                    <PropertyGroup>
                      <global>v2</global>
                    </PropertyGroup>
                    <Message Text=`end:[$(global)]`/>
                  </Target>
                </Project>
            ", logger);
            BuildPropertyGroup globalProperties = new BuildPropertyGroup();
            globalProperties.SetProperty("global", "v0");
            p.GlobalProperties = globalProperties;
            Assertion.AssertEquals("v0", p.GetEvaluatedProperty("global"));
            Assertion.Assert("Project shouldn't be dirty", !p.IsDirtyNeedToReevaluate);
            p.Build(new string[] { "t2" });
            Assertion.Assert("Project shouldn't be dirty", !p.IsDirtyNeedToReevaluate);

            // PropertyGroup outside of target can't overwrite global property,
            // but PropertyGroup inside of target can overwrite it
            logger.AssertLogContains("start:[v0]", "end:[v2]", "final:[v2]");
            Assertion.AssertEquals("v2", p.GetEvaluatedProperty("global"));

            p.ResetBuildStatus();
            Assertion.Assert("Project shouldn't be dirty", !p.IsDirtyNeedToReevaluate);

            // Resetting the project goes back to the old value
            Assertion.AssertEquals("v0", p.GetEvaluatedProperty("global"));
        }


        [Test]
        public void PropertiesAreRevertedAfterBuild()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <p>p0</p>
                  </PropertyGroup>
                  <Target Name=`t`>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            string value = p.GetEvaluatedProperty("p");
            Assertion.AssertEquals("p1", value);

            p.ResetBuildStatus();

            value = p.GetEvaluatedProperty("p");
            Assertion.AssertEquals("p0", value);
        }

        [Test]
        public void PropertiesVisibleToSubsequentTask()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                    <Message Text=`[$(p)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[p1]");
        }

        [Test]
        public void PropertiesVisibleToSubsequentTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`[$(p)]`/>                    
                  </Target>
                  <Target Name=`t`>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t2" });

            logger.AssertLogContains("[p1]");
        }

        [Test]
        public void ItemsVisibleToSubsequentTask()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i Include=`i1`/>
                    </ItemGroup>
                    <Message Text=`[@(i)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[i1]");
        }

        [Test]
        public void ItemsVisibleToSubsequentTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`[@(i)]`/>                    
                  </Target>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i Include=`i1`/>
                    </ItemGroup>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t2" });

            logger.AssertLogContains("[i1]");
        }

        [Test]
        public void ItemsNotVisibleToParallelTargetBatches()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`1.in`><output>1.out</output></i>
                    <i Include=`2.in`><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.output)`>
                    <Message Text=`start:[@(i)]`/>
                    <ItemGroup>
                      <j Include=`%(i.identity)`/>
                    </ItemGroup>
                    <Message Text=`end:[@(j)]`/>                    
                </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "start:[1.in]", "end:[1.in]", "start:[2.in]", "end:[2.in]" });
        }

        [Test]
        public void PropertiesNotVisibleToParallelTargetBatches()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`1.in`><output>1.out</output></i>
                    <i Include=`2.in`><output>2.out</output></i>
                  </ItemGroup>
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.output)`>
                    <Message Text=`start:[$(p)]`/>
                    <PropertyGroup>
                      <p>p1</p>
                    </PropertyGroup>
                    <Message Text=`end:[$(p)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "start:[]", "end:[p1]", "start:[]", "end:[p1]" });
        }

        // One input is built, the other is inferred
        [Test]
        public void ItemsInPartialBuild()
        {
            string[] oldFiles = null, newFiles = null;
            try
            {
                oldFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));
                newFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2006, 1, 1));

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`" + oldFiles[0] + "`><output>" + newFiles[0] + @"</output></i>
                    <i Include=`" + newFiles[1] + "`><output>" + oldFiles[1] + @"</output></i>
                  </ItemGroup> 
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`final:[@(j)]`/>                    
                  </Target>
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.Output)`>
                    <Message Text=`start:[@(j)]`/>
                    <ItemGroup>
                      <j Include=`%(i.identity)`/>
                    </ItemGroup>
                    <Message Text=`end:[@(j)]`/>                    
                </Target>
                </Project>
            ", logger);
                p.Build(new string[] { "t2" });

                // We should only see messages for the out of date inputs, but the itemgroup should do its work for both inputs
                logger.AssertLogContains(new string[] { "start:[]", "end:[" + newFiles[1] + "]", "final:[" + oldFiles[0] + ";" + newFiles[1] + "]" });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(oldFiles);
                ObjectModelHelpers.DeleteTempFiles(newFiles);
            }
        }

        // One input is built, the other input is inferred
        [Test]
        public void PropertiesInPartialBuild()
        {
            string[] oldFiles = null, newFiles = null;
            try
            {
                oldFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));
                newFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2006, 1, 1));

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`" + oldFiles[0] + "`><output>" + newFiles[0] + @"</output></i>
                    <i Include=`" + newFiles[1] + "`><output>" + oldFiles[1] + @"</output></i>
                  </ItemGroup> 
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`final:[$(p)]`/>                    
                  </Target>
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.Output)`>
                    <Message Text=`start:[$(p)]`/>
                    <PropertyGroup>
                      <p>@(i)</p>
                    </PropertyGroup>
                    <Message Text=`end:[$(p)]`/>                    
                </Target>
                </Project>
            ", logger);
                p.Build(new string[] { "t2" });

                // We should only see messages for the out of date inputs, but the propertygroup should do its work for both inputs
                // Finally, execution wins over inferral, as the code chooses to do it that way
                logger.AssertLogContains(new string[] { "start:[]", "end:[" + newFiles[1] + "]", "final:[" + newFiles[1] + "]" });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(oldFiles);
                ObjectModelHelpers.DeleteTempFiles(newFiles);
            }
        }

        // One input is built, the other is inferred
        [Test]
        public void ItemsInPartialBuildVisibleToSubsequentlyInferringTasks()
        {
            string[] oldFiles = null, newFiles = null;
            try
            {
                oldFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));
                newFiles = ObjectModelHelpers.GetTempFiles(2, new DateTime(2006, 1, 1));
                string oldInput = oldFiles[0];
                string newInput = newFiles[1];
                string oldOutput = oldFiles[1];
                string newOutput = newFiles[0];

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`" + oldInput + "`><output>" + newOutput + @"</output></i>
                    <i Include=`" + newInput + "`><output>" + oldOutput + @"</output></i>
                  </ItemGroup> 
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`final:[@(i)]`/>                    
                  </Target>
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.Output)`>
                    <Message Text=`start:[@(i)]`/>
                    <ItemGroup>
                      <j Include=`%(i.identity)`/>
                    </ItemGroup>
                    <Message Text=`middle:[@(i)][@(j)]`/> 
                    <CreateItem Include=`@(j)`>
                      <Output TaskParameter=`Include` ItemName=`i`/>
                    </CreateItem>
                    <Message Text=`end:[@(i)]`/>                    
                </Target>
                </Project>
            ", logger);
                p.Build(new string[] { "t2" });

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

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void IncludeNoOp()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Include=''/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task, null);
        }

        [Test]
        public void RemoveNoOp()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1 Remove='a1'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        [Test]
        public void RemoveItemInTarget()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i1 Include='a1'/> 
                    <i1 Remove='a1'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        /// <summary>
        /// Removes in one batch should never affect adds in a parallel batch, even if that
        /// parallel batch ran first.
        /// </summary>
        [Test]
        public void RemoveOfItemAddedInTargetByParallelTargetBatchDoesNothing()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <!-- just to cause two target batches -->
                    <i Include=`1.in`><output>1.out</output></i>
                    <i Include=`2.in`><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.output)`>
                    <ItemGroup>
                      <j Include=`a` Condition=`'%(i.Identity)'=='1.in'`/>
                      <j Remove=`a` Condition=`'%(i.Identity)'=='2.in'`/>

                      <!-- and again in reversed batch order, in case the engine batches the other way around -->
                      <j Include=`b` Condition=`'%(i.Identity)'=='2.in'`/>
                      <j Remove=`b` Condition=`'%(i.Identity)'=='1.in'`/>

                      <!-- but obviously a remove in the same batch works -->
                      <j Include=`c` Condition=`'%(i.Identity)'=='2.in'`/>
                      <j Remove=`c` Condition=`'%(i.Identity)'=='2.in'`/>

                      <!-- unless it's before the add -->
                      <j Remove=`d` Condition=`'%(i.Identity)'=='2.in'`/>
                      <j Include=`d` Condition=`'%(i.Identity)'=='2.in'`/>
                  </ItemGroup>
                  </Target>
                  <Target Name=`t2`>
                    <Message Text=`final:[@(j)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t", "t2" });

            logger.AssertLogContains(new string[] { "final:[a;b;d]" });
        }

        [Test]
        public void RemoveItemInTargetWithTransform()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i0 Include='a.cpp;b.cpp'/>
                    <i1 Include='a.obj;b.obj'/> 
                    <i1 Remove=""@(i0->'%(filename).obj')""/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        [Test]
        public void RemoveWithMultipleItemspecs()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i1 Include='a1'/> 
                    <i1 Include='a2'/> 
                    <i1 Remove='a1;a2'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        [Test]
        public void RemoveAllItemsInList()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i1 Include='a1'/> 
                    <i1 Include='a2'/> 
                    <i1 Remove='@(i1)'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        [Test]
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i0 Remove='a2'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = GenerateLookup();

            task.ExecuteTask(lookup);

            BuildItemGroup i0Group = lookup.GetItems("i0");

            Assertion.AssertEquals(3, i0Group.Count);
            Assertion.AssertEquals("a1", i0Group[0].FinalItemSpec);
            Assertion.AssertEquals("a3", i0Group[1].FinalItemSpec);
            Assertion.AssertEquals("a4", i0Group[2].FinalItemSpec);
        }

        /// <summary>
        /// Bare (batchable) metadata is prohibited on IG/PG conditions -- all other expressions 
        /// should be allowed
        /// </summary>
        [Test]
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <PropertyGroup Condition=""'$(p0)'=='v0' and '@(i0)'=='a1;a2;a3;a4' and '@(i0->'%(identity).x','|')'=='a1.x|a2.x|a3.x|a4.x'""> 
                  <p1>v1</p1>
                </PropertyGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookupWithItemsAndProperties();

            task.ExecuteTask(lookup);

            string p1 = lookup.GetProperty("p1").FinalValue;

            Assertion.AssertEquals("v1", p1);
        }

        /// <summary>
        /// Bare (batchable) metadata is prohibited on IG/PG conditions -- all other expressions 
        /// should be allowed
        /// </summary>
        [Test]
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup Condition=""'$(p0)'=='v0' and '@(i0)'=='a1;a2;a3;a4' and '@(i0->'%(identity).x','|')'=='a1.x|a2.x|a3.x|a4.x'"">  
                  <i1 Include='x'/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookupWithItemsAndProperties();

            task.ExecuteTask(lookup);

            BuildItemGroup i1Group = lookup.GetItems("i1");

            Assertion.AssertEquals(1, i1Group.Count);
            Assertion.AssertEquals("x", i1Group[0].FinalItemSpec);
        }

        /// <summary>
        /// This bug was caused by batching over the ItemGroup as well as over each child.
        /// If the condition on a child did not exclude it, an unwitting child could be included multiple times,
        /// once for each outer batch. The fix was to abandon the idea of outer batching and just 
        /// prohibit batchable expressions on the ItemGroup conditions. It's just too hard to write such expressions
        /// in a comprehensible way.    
        /// </summary>
        [Test]
        public void RegressPCHBug68578()
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                  <!-- squint and pretend i0 is 'CppCompile' and 'm' is 'ObjectFile' -->
                  <Link Include=""A_PCH""/>
                  <Link Include=""@(i0->'%(m).obj')"" Condition=""'%(i0.m)' == 'm1'""/>
                  <Link Include=""@(i0->'%(m)')"" Condition=""'%(i0.m)' == 'm2'""/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookup();

            task.ExecuteTask(lookup);

            BuildItemGroup linkGroup = lookup.GetItems("link");

            Assertion.AssertEquals(4, linkGroup.Count);
            Assertion.AssertEquals("A_PCH", linkGroup[0].FinalItemSpec);
            Assertion.AssertEquals("m1.obj", linkGroup[1].FinalItemSpec);
            Assertion.AssertEquals("m2", linkGroup[2].FinalItemSpec);
            Assertion.AssertEquals("m2", linkGroup[3].FinalItemSpec);
        }

        [Test]
        public void RemovesOfPersistedItemsAreReversed()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='a1'/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0 Remove=`a1`/>
                    </ItemGroup>
                    <Message Text=`[@(i0)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            // The item was removed during the build
            logger.AssertLogContains("[]");
            Assertion.AssertEquals(0, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(0, p.EvaluatedItems.Count);

            p.ResetBuildStatus();
            // We should still have the item left
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(1, p.EvaluatedItems.Count);
        }

        [Test]
        public void RemovesOfPersistedItemsAreReversed1()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='a1'/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0 Include='a1'/>
                      <i0 Remove=`a1`/>
                    </ItemGroup>
                    <Message Text=`[@(i0)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[]");
            Assertion.AssertEquals(0, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(0, p.EvaluatedItems.Count);

            p.ResetBuildStatus();
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(1, p.EvaluatedItems.Count);
        }

        [Test]
        public void RemovesOfPersistedItemsAreReversed2()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='a1'/>
                    <i0 Include='a2'/>
                    <i1 Include='b1'/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0 Include='a1'/>
                      <i0 Remove=`a1`/>
                      <i0 Include='a1'/>
                      <i0 Include='a3'/>
                    </ItemGroup>
                    <Message Text=`[@(i0)][@(i1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[a2;a1;a3][b1]");
            Assertion.AssertEquals(3, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i1"]).Count);
            Assertion.AssertEquals(4, p.EvaluatedItems.Count);

            p.ResetBuildStatus();
            Assertion.AssertEquals(2, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i1"]).Count);
            Assertion.AssertEquals(3, p.EvaluatedItems.Count);
        }

        [Test]
        public void RemovesOfPersistedItemsAreReversed3()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='a1'>
                      <m>m1</m>
                    </i0> 
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0 Include='a1'>
                        <m>m2</m>
                      </i0> 
                      <i0 Remove=`a1`/>
                    </ItemGroup>
                    <Message Text=`[%(i0.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[]");
            Assertion.AssertEquals(0, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(0, p.EvaluatedItems.Count);

            p.ResetBuildStatus();
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals("m1", ((BuildItemGroup)p.EvaluatedItemsByName["i0"])[0].GetMetadata("m"));
            Assertion.AssertEquals(1, p.EvaluatedItems.Count);
        }

        /// <summary>
        /// Persisted item is copied into another item list by an ItemGroup -- the copy
        /// should be reversed
        /// </summary>
        [Test]
        public void RemovesOfPersistedItemsAreReversed4()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='a1'/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0 Include='@(i0)'/>
                      <i1 Include='@(i0)'/> <!-- for good measure, into another list as well -->
                    </ItemGroup>
                    <Message Text=`[@(i0)][@(i1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[a1;a1][a1;a1]");
            Assertion.AssertEquals(2, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(2, ((BuildItemGroup)p.EvaluatedItemsByName["i1"]).Count);
            Assertion.AssertEquals(4, p.EvaluatedItems.Count);

            p.ResetBuildStatus();
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals("a1", ((BuildItemGroup)p.EvaluatedItemsByName["i0"])[0].EvaluatedItemSpec);
            Assertion.AssertEquals(0, ((BuildItemGroup)p.EvaluatedItemsByName["i1"]).Count);
            Assertion.AssertEquals(1, p.EvaluatedItems.Count);
        }

        [Test]
        public void RemovesOfItemsOnlyWithMetadataValue()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='a1'>
                      <m>m1</m>
                    </i0> 
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0 Include='a1'>
                        <m>m2</m>
                      </i0> 
                      <i0 Remove=`a1` Condition=`'%(i0.m)' == 'm1'`/>
                    </ItemGroup>
                    <Message Text=`[%(i0.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[m2]");
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
        }

        [Test]
        public void RemoveBatchingOnRemoveValue()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='m1;m2;m3'/>
                    <i1 Include='a1'>
                      <m>m1</m>
                    </i1>
                    <i1 Include='a2'>
                      <m>m2</m>
                    </i1>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0 Remove=`%(i1.m)`/>
                    </ItemGroup>
                    <Message Text=`[@(i0)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[m3]");
            Assertion.AssertEquals(1, ((BuildItemGroup)p.EvaluatedItemsByName["i0"]).Count);
        }

        [Test]
        public void RemoveWithWildcards()
        {
            string[] files = null;

            try
            {
                files = ObjectModelHelpers.GetTempFiles(2, DateTime.Now);

                string content = @"
                <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <ItemGroup>
                        <i1 Include='" + files[0] + ";" + files[1] + @";other'/> 
                        <i1 Remove='$(temp)\*.tmp'/>
                    </ItemGroup>
                </Target>";
                IntrinsicTask task = CreateIntrinsicTask(content);
                BuildPropertyGroup properties = new BuildPropertyGroup();
                properties.SetProperty("TEMP", Environment.GetEnvironmentVariable("TEMP"));
                Lookup lookup = LookupHelpers.CreateLookup(properties);
                ExecuteTask(task, lookup);

                Assertion.AssertEquals(1, lookup.GetItems("i1").Count);
                Assertion.AssertEquals("other", lookup.GetItems("i1")[0].FinalItemSpec);
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(files);
            }
        }

        [Test]
        public void RemovesNotVisibleToParallelTargetBatches()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`1.in`><output>1.out</output></i>
                    <i Include=`2.in`><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.output)`>
                    <Message Text=`start:[@(i)]`/>
                    <ItemGroup>
                      <i Remove=`1.in;2.in`/>
                    </ItemGroup>
                    <Message Text=`end:[@(i)]`/>                    
                </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "start:[1.in]", "end:[]", "start:[2.in]", "end:[]" });
        }

        [Test]
        public void RemovesNotVisibleToParallelTargetBatches2()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`1.in`><output>1.out</output></i>
                    <i Include=`2.in`><output>2.out</output></i>
                    <j Include=`j1`/>
                  </ItemGroup> 
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.output)`>
                    <Message Text=`start:[@(j)]`/>
                    <ItemGroup>
                      <j Remove=`@(j)`/>
                    </ItemGroup>
                    <Message Text=`end:[@(j)]`/>                    
                </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "start:[j1]", "end:[]", "start:[j1]", "end:[]" });
        }

#if false // Not implemented yet: this was working when we were cloning, but now needs some thought.

        /// <summary>
        /// The historical task output publishing model prevents a called target seeing outputs
        /// from tasks in the same target that have already run. We choose to not follow this model
        /// for itemgroups in targets.
        /// </summary>
        [Test]
        public void RemovesAreVisibleToCalledTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`i1;i2`/>
                  </ItemGroup> 
                  <Target Name=`t`>
                    <Message Text=`a:[@(i)]`/>
                    <ItemGroup>
                      <i Remove=`i2`/>
                    </ItemGroup>
                    <Message Text=`b:[@(i)]`/>
                    <CallTarget Targets=`t2`/>
                    <Message Text=`d:[@(i)]`/>                    
                  </Target>
                  <Target Name=`t2`>
                    <Message Text=`c:[@(i)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "a:[i1;i2]", "b:[i1]", "c:[i1]", "d:[i1]" });
        }
#endif

        /// <summary>
        /// Whidbey behavior was that items/properties emitted by a target being called, were
        /// not visible to subsequent tasks in the calling target. (That was because the project
        /// items and properties had been cloned for the target batches.) We must match that behavior.
        /// </summary>
        [Test]
        public void CalledTargetItemsAreNotVisibleToCallerTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            ", logger);
            p.Build(new string[] { "t3" });

            logger.AssertLogContains(new string[] { "in target:[a][a]", "after target:[a;b;c][a;b;c]" });
        }

        /// <summary>
        /// Whidbey behavior was that items/properties emitted by a target calling another target, were
        /// not visible to the calling target. (That was because the project items and properties had been cloned for the target batches.) 
        /// We must match that behavior. (For now)
        /// </summary>
        [Test]
        public void CallerTargetItemsAreNotVisibleToCalledTarget()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            ", logger);
            p.Build(new string[] { "t3" });

            logger.AssertLogContains(new string[] { "in target:[a][a]", "after target:[a;b;c][a;b;c]" });
        }

        [Test]
        public void ModifyNoOp()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup> 
                    <i1/>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            Assertion.AssertEquals(0, lookup.GetItems("i1").Count);
        }

        [Test]
        public void ModifyItemInTarget()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i1 Include='a1'> 
                      <m>m1</m>
                    </i1>
                    <i1>
                      <m>m2</m>
                    </i1>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItem item = lookup.GetItems("i1")[0];
            Assertion.AssertEquals("m2", item.GetMetadata("m"));
        }

        [Test]
        public void ModifyItemInTargetLastMetadataWins()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItem item = lookup.GetItems("i1")[0];
            Assertion.AssertEquals("m3", item.GetMetadata("m"));
        }

        [Test]
        public void ModifyItemEmittedByTask()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <CreateItem Include='a1' AdditionalMetadata='m=m1;n=n1'>
                      <Output TaskParameter='include' ItemName='i1'/>
                    </CreateItem>
                    <ItemGroup>
                      <i1>
                        <m>m2</m>
                      </i1>
                    </ItemGroup>
                    <Message Text=`[%(i1.m)][%(i1.n)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "[m2][n1]" });
        }

        [Test]
        public void ModifyItemInTargetWithCondition()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItem item1 = lookup.GetItems("i1")[0];
            BuildItem item2 = lookup.GetItems("i1")[1];
            Assertion.AssertEquals("a1", item1.FinalItemSpec);
            Assertion.AssertEquals("a2", item2.FinalItemSpec);
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));
            Assertion.AssertEquals("m3", item2.GetMetadata("m"));
        }

        [Test]
        public void ModifyItemInTargetWithConditionOnMetadata()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItem item1 = lookup.GetItems("i1")[0];
            BuildItem item2 = lookup.GetItems("i1")[1];
            Assertion.AssertEquals("a1", item1.FinalItemSpec);
            Assertion.AssertEquals("a2", item2.FinalItemSpec);
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));
            Assertion.AssertEquals("m3", item2.GetMetadata("m"));
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ModifyItemWithUnqualifiedMetadataError()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i1 Include='a1'/>
                    <i1>
                      <m Condition=""'%(undefined_on_a1)'=='1'"">2</m>
                    </i1>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            ExecuteTask(task, null);
        }

        [Test]
        public void ModifyItemInTargetWithConditionWithoutItemTypeOnMetadataInCondition()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItem item1 = lookup.GetItems("i1")[0];
            BuildItem item2 = lookup.GetItems("i1")[1];
            Assertion.AssertEquals("a1", item1.FinalItemSpec);
            Assertion.AssertEquals("a2", item2.FinalItemSpec);
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));
            Assertion.AssertEquals("m3", item2.GetMetadata("m"));
        }


        [Test]
        public void ModifyItemInTargetWithConditionOnMetadataWithoutItemTypeOnMetadataInCondition()
        {
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
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
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);
            Lookup lookup = LookupHelpers.CreateEmptyLookup();
            ExecuteTask(task, lookup);

            BuildItem item1 = lookup.GetItems("i1")[0];
            BuildItem item2 = lookup.GetItems("i1")[1];
            Assertion.AssertEquals("a1", item1.FinalItemSpec);
            Assertion.AssertEquals("a2", item2.FinalItemSpec);
            Assertion.AssertEquals("m1", item1.GetMetadata("m"));
            Assertion.AssertEquals("m3", item2.GetMetadata("m"));
        }

        [Test]
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
            string content = @"
            <Target Name='t' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                <ItemGroup>
                    <i0>
                      <m>m4</m>
                    </i0>
                </ItemGroup>
            </Target>";
            IntrinsicTask task = CreateIntrinsicTask(content);

            Lookup lookup = GenerateLookup();
            
            task.ExecuteTask(lookup);

            BuildItemGroup i0Group = lookup.GetItems("i0");

            Assertion.AssertEquals(4, i0Group.Count);
            foreach (BuildItem item in i0Group)
            {
                item.EvaluateAllItemMetadata(new Expander(new BuildPropertyGroup()), ParserOptions.AllowAll, null, null);
                Assertion.AssertEquals("m4", item.GetEvaluatedMetadata("m"));
            }
        }

        [Test]
        public void RemoveComplexMidlExample()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
  <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
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
       <Idl Include=`a.idl`/>
       <Idl Include=`b.idl`>
          <DllDataFileName>mydlldata.c</DllDataFileName>
       </Idl>
       <Idl Include=`c.idl`>
          <HeaderFileName>myheader.h</HeaderFileName>
       </Idl>
    </ItemGroup>

    <Target Name=`MIDL`>
        <ItemGroup>
          <Idl>
            <DllDataFileName Condition=`'$(UseIdlBasedDllData)' == 'true' and '%(Idl.DllDataFileName)' == ''`>$(MidlDllDataDir)\%(Filename)_dlldata.c</DllDataFileName>
            <DllDataFileName Condition=`'$(UseIdlBasedDllData)' != 'true' and '%(Idl.DllDataFileName)' == ''`>$(MidlDllDataFileName)</DllDataFileName>
            <HeaderFileName Condition=`'%(Idl.HeaderFileName)' == ''`>$(MidlHeaderDir)\%(Idl.Filename).h</HeaderFileName>
            <TypeLibraryName Condition=`'%(Idl.TypeLibraryName)' == ''`>$(MidlTlbDir)\%(Filename).tlb</TypeLibraryName>
            <ProxyFileName Condition=`'%(Idl.ProxyFileName)' == ''`>$(MidlProxyDir)\%(Filename)_p.c</ProxyFileName>
            <InterfaceIdentifierFileName Condition=`'%(Idl.InterfaceIdentifierFileName)' == ''`>$(MidlInterfaceDir)\%(Filename)_i.c</InterfaceIdentifierFileName>
          </Idl>
        </ItemGroup>

        <Message Text=`[%(idl.identity)|%(idl.dlldatafilename)|%(idl.headerfilename)|%(idl.TypeLibraryName)|%(idl.ProxyFileName)|%(idl.InterfaceIdentifierFileName)]`/>
    </Target>
  </Project>
            ", logger);
            p.Build(new string[] { "MIDL" });

            logger.AssertLogContains(@"[a.idl|dlldatadir\a_dlldata.c|headerdir\a.h|tlbdir\a.tlb|proxydir\a_p.c|interfacedir\a_i.c]",
                                     @"[b.idl|mydlldata.c|headerdir\b.h|tlbdir\b.tlb|proxydir\b_p.c|interfacedir\b_i.c]",
                                     @"[c.idl|dlldatadir\c_dlldata.c|myheader.h|tlbdir\c.tlb|proxydir\c_p.c|interfacedir\c_i.c]");
        }

        [Test]
        public void ModifiesOfPersistedItemsAreReversed1()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='i1'>
                      <m>m0</m>
                    </i0>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i0>
                        <m>m1</m>
                      </i0> 
                    </ItemGroup>
                  </Target>
                  <Target Name=`t2`>
                    <Message Text='[%(i0.m)]'/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t", "t2" });

            logger.AssertLogContains("[m1]");

            BuildItem item = ((BuildItemGroup)p.EvaluatedItemsByName["i0"])[0];
            Assertion.AssertEquals("m1", item.GetEvaluatedMetadata("m"));

            p.ResetBuildStatus();
            Assertion.AssertEquals("m0", item.GetEvaluatedMetadata("m"));
        }

        /// <summary>
        /// Modify of an item copied during the build
        /// </summary>
        [Test]
        public void ModifiesOfPersistedItemsAreReversed2()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i0 Include='i1'>
                      <m>m0</m>
                      <n>n0</n>
                    </i0>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i1 Include='@(i0)'>
                        <m>m1</m>
                      </i1>
                      <i1>
                        <n>n1</n>
                      </i1> 
                    </ItemGroup>
                  </Target>
                  <Target Name=`t2`>
                    <Message Text='[%(i0.m)][%(i0.n)]'/>
                    <Message Text='[%(i1.m)][%(i1.n)]'/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t", "t2" });

            logger.AssertLogContains("[m0][n0]", "[m1][n1]");

            Assertion.AssertEquals(1, ((BuildItemGroup)p.evaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(1, ((BuildItemGroup)p.evaluatedItemsByName["i1"]).Count);
            Assertion.AssertEquals("m0", ((BuildItemGroup)p.EvaluatedItemsByName["i0"])[0].GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("n0", ((BuildItemGroup)p.EvaluatedItemsByName["i0"])[0].GetEvaluatedMetadata("n"));
            Assertion.AssertEquals("m1", ((BuildItemGroup)p.EvaluatedItemsByName["i1"])[0].GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("n1", ((BuildItemGroup)p.EvaluatedItemsByName["i1"])[0].GetEvaluatedMetadata("n"));

            p.ResetBuildStatus();
            Assertion.AssertEquals(1, ((BuildItemGroup)p.evaluatedItemsByName["i0"]).Count);
            Assertion.AssertEquals(0, ((BuildItemGroup)p.evaluatedItemsByName["i1"]).Count);
            Assertion.AssertEquals("m0", ((BuildItemGroup)p.EvaluatedItemsByName["i0"])[0].GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("n0", ((BuildItemGroup)p.EvaluatedItemsByName["i0"])[0].GetEvaluatedMetadata("n"));
        }

        [Test]
        public void RemoveItemInImportedFile()
        {
            MockLogger logger = new MockLogger();
            string importedFile = null;

            try
            {
                importedFile = Path.GetTempFileName();
                File.WriteAllText(importedFile, @"
                <Project ToolsVersion='3.5' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <ItemGroup>
                    <i1 Include='imported'/>
                  </ItemGroup>
                </Project>
            ");
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                      <Import Project='" + importedFile + @"'/>
                      <Target Name=`t`>
                        <Message Text=`[@(i1)]`/>
                        <ItemGroup>
                          <i1 Remove=`imported`/>
                        </ItemGroup>
                        <Message Text=`[@(i1)]`/>
                      </Target>
                    </Project>
                ", logger);
                p.Build(new string[] { "t" });

                logger.AssertLogContains("[imported]", "[]");
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(new string[] { importedFile });
            }
        }

        [Test]
        public void ModifyItemInImportedFile()
        {
            MockLogger logger = new MockLogger();
            string importedFile = null;

            try
            {
                importedFile = Path.GetTempFileName();
                File.WriteAllText(importedFile, @"
                <Project ToolsVersion='3.5' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <ItemGroup>
                    <i1 Include='imported'/>
                  </ItemGroup>
                </Project>
            ");
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                      <Import Project='" + importedFile + @"'/>
                      <Target Name=`t`>
                        <ItemGroup>
                          <i1>
                            <m>m1</m>
                          </i1>
                        </ItemGroup>
                        <Message Text=`[%(i1.m)]`/>
                      </Target>
                    </Project>
                ", logger);
                p.Build(new string[] { "t" });

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
        [Test]
        public void OutputPropertiesInTargetBatchesCreateItem()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <!-- just to cause two target batches -->
                    <i Include=`1.in`><output>1.out</output></i>
                    <i Include=`2.in`><output>2.out</output></i>
                  </ItemGroup> 
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.output)`>
                    <Message Text=`start:[$(p)]`/>
                    <CreateProperty Value='$(p)--%(i.Identity)'>
                      <Output TaskParameter='Value' PropertyName='p'/>
                    </CreateProperty>
                    <Message Text=`end:[$(p)]`/>
                  </Target>
                  <Target Name=`t2`>
                    <Message Text=`final:[$(p)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t", "t2" });

            logger.AssertLogContains(new string[] { "start:[]", "end:[--1.in]", "start:[]", "end:[--2.in]", "final:[--2.in]" });
        }

        /// <summary>
        /// Properties produced in one task batch are not visible to another
        /// </summary>
        [Test]
        public void OutputPropertiesInTaskBatchesCreateItem()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <ItemGroup>
                      <i Include=`1.in;2.in`/>
                    </ItemGroup>
                    <CreateProperty Value='$(p)--%(i.Identity)'>
                      <Output TaskParameter='Value' PropertyName='p'/>
                    </CreateProperty>
                    <Message Text=`end:[$(p)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains(new string[] { "end:[--2.in]" });
        }

        [Test]
        public void PropertiesInInferredBuildCreateProperty()
        {
            string[] files = null;
            try
            {
                files = ObjectModelHelpers.GetTempFiles(2, new DateTime(2005, 1, 1));

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`" + files[0] + "`><output>" + files[1] + @"</output></i>
                  </ItemGroup> 
                  <Target Name=`t2` DependsOnTargets=`t`>
                    <Message Text=`final:[$(p)]`/>                    
                  </Target>
                  <Target Name=`t` Inputs=`%(i.Identity)` Outputs=`%(i.Output)`>
                    <Message Text=`start:[$(p)]`/>
                    <CreateProperty Value='@(i)'>
                      <Output TaskParameter='Value' PropertyName='p'/>
                    </CreateProperty>
                    <Message Text=`end:[$(p)]`/>                    
                </Target>
                </Project>
            ", logger);
                p.Build(new string[] { "t2" });

                // We should only see messages from the second target, as the first is only inferred
                logger.AssertLogDoesntContain("start:");
                logger.AssertLogDoesntContain("end:");
                logger.AssertLogContains(new string[] { "final:[" + files[0] + "]" });
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(files);
            }
        }

        [Test]
        public void ModifyItemPreviouslyModified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`a`/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x>
                        <m1>2</m1>
                      </x>  
                    </ItemGroup>
                    <Message Text=`[%(x.m1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Test]
        public void ModifyItemPreviouslyModified2()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`a`/>
                  </ItemGroup>
                  <Target Name=`t`>
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
                    <Message Text=`[%(x.m1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Test]
        public void RemoveItemPreviouslyModified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`a`/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x Remove=`@(x)`/>
                    </ItemGroup>
                    <Message Text=`[%(x.m1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogDoesntContain("[2]");
        }

        [Test]
        public void RemoveItemPreviouslyModified2()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`a`/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                    </ItemGroup>
                    <ItemGroup>
                      <x Remove=`@(x)`/>
                    </ItemGroup>
                    <Message Text=`[%(x.m1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogDoesntContain("[2]");
        }

        [Test]
        public void FilterItemPreviouslyModified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`a`/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x Condition=`'%(x.m1)'=='1'`>
                        <m1>2</m1>
                      </x>  
                    </ItemGroup>
                    <Message Text=`[%(x.m1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Test]
        public void FilterItemPreviouslyModified2()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <x Include=`a`/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <ItemGroup>
                      <x>
                        <m1>1</m1>
                      </x>
                      <x>
                        <m1 Condition=`'%(x.m1)'=='1'`>2</m1>
                      </x>  
                    </ItemGroup>
                    <Message Text=`[%(x.m1)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogDoesntContain("[1]");
            logger.AssertLogContains("[2]");
        }

        [Test]
        public void FilterItemPreviouslyModified3()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <ItemGroup>
                       <A Include=`a;b;c`>
                           <m>m1</m>
                       </A>
                   </ItemGroup>
                   <Target Name=`t`>
                       <ItemGroup>
                           <A Condition=`'%(m)' == 'm1'`>
                               <m>m2</m>
                           </A>
                       </ItemGroup>
                       <ItemGroup>
                           <A Condition=`'%(m)' == 'm2'`>
                               <m>m3</m>
                           </A>
                       </ItemGroup>
                       <ItemGroup>
                           <A Condition=`'%(m)' == 'm3'`>
                               <m>m4</m>
                           </A>
                       </ItemGroup>
                       <Message Text=`[@(A) = %(A.m)]`/>
                   </Target>
                </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[a;b;c = m4]");
        }

        [Test]
        public void FilterItemPreviouslyModified4()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <Target Name=`t`>
                       <ItemGroup>
                           <A Include=`a;b;c`>
                               <m>m1</m>
                           </A>
                           <A Condition=`'%(Identity)' == 'a' or '%(Identity)' == 'c'`>
                               <m>m2</m>
                           </A>
                           <A Condition=`'%(Identity)' == 'a' or '%(Identity)' == 'c'`>
                               <m>m3</m>
                           </A>
                       </ItemGroup>
                       <Message Text=`[@(A) = %(A.m)]`/>
                   </Target>
               </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[b = m1]");
            logger.AssertLogContains("[a;c = m3]");
        }

        [Test]
        public void FilterItemPreviouslyModified5()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                   <Target Name=`t`>
                       <ItemGroup>
                           <A Include=`a;b;c`>
                               <m>m1</m>
                           </A>
                           <A Condition=`'%(Identity)' == 'a' or '%(Identity)' == 'c'`>
                               <m>m2</m>
                           </A>
                           <A Condition=`'%(Identity)' == 'a'`>
                               <m>m3</m>
                           </A>
                       </ItemGroup>
                       <Message Text=`[@(A) = %(A.m)]`/>
                   </Target>
               </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[a = m3]");
            logger.AssertLogContains("[b = m1]");
            logger.AssertLogContains("[c = m2]");
        }

        [Test]
        public void FilterItemPreviouslyModified6()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemGroup>
                        <A Include=`a;b;c`>
                            <m>m1</m>
                        </A>
                    </ItemGroup>
                    <Target Name=`t`>
                        <ItemGroup>
                            <A Condition=`'%(m)' == 'm1'`>
                                <m>m2</m>
                            </A>
                        </ItemGroup>
                        <ItemGroup>
                            <A Condition=`'%(m)' == 'm2'`>
                                <m></m>
                            </A>
                        </ItemGroup>
                        <ItemGroup>
                            <A Condition=`'%(m)' == 'm3'`>
                                <m>m3</m>
                            </A>
                        </ItemGroup>
                        <Message Text=`[@(A)=%(A.m)]`/>
                    </Target>
               </Project>
            ", logger);
            p.Build(new string[] { "t" });

            logger.AssertLogContains("[a;b;c=]");
        }
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Helpers

        private static BuildPropertyGroup GeneratePropertyGroup()
        {
            BuildPropertyGroup properties = new BuildPropertyGroup();
            properties.SetProperty("p0", "v0");
            return properties;
        }

        private static Lookup GenerateLookupWithItemsAndProperties()
        {
            BuildPropertyGroup pg = new BuildPropertyGroup();
            pg.SetProperty("p0", "v0");

            Lookup lookup = GenerateLookup(pg);
            return lookup;
        }

        private static Lookup GenerateLookup()
        {
            return GenerateLookup(new BuildPropertyGroup());
        }

        private static Lookup GenerateLookup(BuildPropertyGroup properties)
        {
            BuildItemGroup items = new BuildItemGroup();
            BuildItem item1 = new BuildItem("i0", "a1");
            BuildItem item2 = new BuildItem("i0", "a2");
            BuildItem item3 = new BuildItem("i0", "a3");
            BuildItem item4 = new BuildItem("i0", "a4");
            item1.SetMetadata("m", "m1");
            item1.SetMetadata("n", "n1");
            item2.SetMetadata("m", "m2");
            item2.SetMetadata("n", "n2");
            item3.SetMetadata("m", "m2");
            item3.SetMetadata("n", "n2");
            item4.SetMetadata("m", "m3");
            item4.SetMetadata("n", "n3");
            items.AddItem(item1);
            items.AddItem(item2);
            items.AddItem(item3);
            items.AddItem(item4);
            Hashtable itemsByName = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemsByName.Add("i0", items);

            Lookup lookup = LookupHelpers.CreateLookup(properties, itemsByName);

            return lookup;
        }

        private static IntrinsicTask CreateIntrinsicTask(string content)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content);

            IntrinsicTask task = new IntrinsicTask((XmlElement)doc.FirstChild.FirstChild, 
                                                   new EngineLoggingServicesInProc(new EventSource(), true, null), 
                                                   null, 
                                                   Directory.GetCurrentDirectory(),
                                                   new ItemDefinitionLibrary(new Project()));
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

        #endregion
    }
}
