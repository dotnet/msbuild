// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;
using System.Linq;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectItemInstance public members
    /// </summary>
    public class ProjectItemInstance_Tests
    {
        /// <summary>
        /// The number of built-in metadata for items.
        /// </summary>
        public const int BuiltInMetadataCount = 15;

        /// <summary>
        /// Basic ProjectItemInstance without metadata
        /// </summary>
        [Fact]
        public void AccessorsWithoutMetadata()
        {
            ProjectItemInstance item = GetItemInstance();

            Assert.Equal("i", item.ItemType);
            Assert.Equal("i1", item.EvaluatedInclude);
            Assert.False(item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Basic ProjectItemInstance with metadata
        /// </summary>
        [Fact]
        public void AccessorsWithMetadata()
        {
            ProjectItemInstance item = GetItemInstance();

            item.SetMetadata("m1", "v0");
            item.SetMetadata("m1", "v1");
            item.SetMetadata("m2", "v2");

            Assert.Equal("m1", item.GetMetadata("m1").Name);
            Assert.Equal("m2", item.GetMetadata("m2").Name);
            Assert.Equal("v1", item.GetMetadataValue("m1"));
            Assert.Equal("v2", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Get metadata not present
        /// </summary>
        [Fact]
        public void GetMissingMetadata()
        {
            ProjectItemInstance item = GetItemInstance();
            Assert.Null(item.GetMetadata("X"));
            Assert.Equal(String.Empty, item.GetMetadataValue("X"));
        }

        /// <summary>
        /// Set include
        /// </summary>
        [Fact]
        public void SetInclude()
        {
            ProjectItemInstance item = GetItemInstance();
            item.EvaluatedInclude = "i1b";
            Assert.Equal("i1b", item.EvaluatedInclude);
        }

        /// <summary>
        /// Set include to empty string
        /// </summary>
        [Fact]
        public void SetInvalidEmptyInclude()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.EvaluatedInclude = String.Empty;
            }
           );
        }
        /// <summary>
        /// Set include to invalid null value
        /// </summary>
        [Fact]
        public void SetInvalidNullInclude()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.EvaluatedInclude = null;
            }
           );
        }
        /// <summary>
        /// Create an item with a metadatum that has a null value
        /// </summary>
        [Fact]
        public void CreateItemWithNullMetadataValue()
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();

            IDictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("m", null);

            ProjectItemInstance item = projectInstance.AddItem("i", "i1", metadata);
            Assert.Equal(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata value
        /// </summary>
        [Fact]
        public void SetMetadata()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", "m1");
            Assert.Equal("m1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata value to empty string
        /// </summary>
        [Fact]
        public void SetMetadataEmptyString()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", String.Empty);
            Assert.Equal(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata value to null value -- this is allowed, but 
        /// internally converted to the empty string. 
        /// </summary>
        [Fact]
        public void SetNullMetadataValue()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", null);
            Assert.Equal(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata with invalid empty name
        /// </summary>
        [Fact]
        public void SetInvalidNullMetadataName()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.SetMetadata(null, "m1");
            }
           );
        }
        /// <summary>
        /// Set metadata with invalid empty name
        /// </summary>
        [Fact]
        public void SetInvalidEmptyMetadataName()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.SetMetadata(String.Empty, "m1");
            }
           );
        }
        /// <summary>
        /// Cast to ITaskItem
        /// </summary>
        [Fact]
        public void CastToITaskItem()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", "m1");

            ITaskItem taskItem = (ITaskItem)item;

            Assert.Equal(item.EvaluatedInclude, taskItem.ItemSpec);
            Assert.Equal(1 + BuiltInMetadataCount, taskItem.MetadataCount);
            Assert.Equal(1 + BuiltInMetadataCount, taskItem.MetadataNames.Count);
            Assert.Equal("m1", taskItem.GetMetadata("m"));
            taskItem.SetMetadata("m", "m2");
            Assert.Equal("m2", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Creates a ProjectItemInstance and casts it to ITaskItem2; makes sure that all escaped information is
        /// maintained correctly.  Also creates a new Microsoft.Build.Utilities.TaskItem from the ProjectItemInstance
        /// and verifies that none of the information is lost.  
        /// </summary>
        [Fact]
        public void ITaskItem2Operations()
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();

            ProjectItemInstance item = projectInstance.AddItem("EscapedItem", "esca%20ped%3bitem");
            item.SetMetadata("m", "m1");
            item.SetMetadata("m;", "m%3b1");
            ITaskItem2 taskItem = (ITaskItem2)item;

            Assert.Equal("esca%20ped%3bitem", taskItem.EvaluatedIncludeEscaped);
            Assert.Equal("esca ped;item", taskItem.ItemSpec);

            Assert.Equal("m;1", taskItem.GetMetadata("m;"));
            Assert.Equal("m%3b1", taskItem.GetMetadataValueEscaped("m;"));
            Assert.Equal("m1", taskItem.GetMetadataValueEscaped("m"));

            Assert.Equal("esca%20ped%3bitem", taskItem.EvaluatedIncludeEscaped);
            Assert.Equal("esca ped;item", taskItem.ItemSpec);

            ITaskItem2 taskItem2 = new Microsoft.Build.Utilities.TaskItem(taskItem);

            taskItem2.SetMetadataValueLiteral("m;", "m;2");

            Assert.Equal("m%3b2", taskItem2.GetMetadataValueEscaped("m;"));
            Assert.Equal("m;2", taskItem2.GetMetadata("m;"));

            IDictionary<string, string> taskItem2Metadata = (IDictionary<string, string>)taskItem2.CloneCustomMetadata();
            Assert.Equal(3, taskItem2Metadata.Count);

            foreach (KeyValuePair<string, string> pair in taskItem2Metadata)
            {
                if (pair.Key.Equals("m"))
                {
                    Assert.Equal("m1", pair.Value);
                }

                if (pair.Key.Equals("m;"))
                {
                    Assert.Equal("m;2", pair.Value);
                }

                if (pair.Key.Equals("OriginalItemSpec"))
                {
                    Assert.Equal("esca ped;item", pair.Value);
                }
            }

            IDictionary<string, string> taskItem2MetadataEscaped = (IDictionary<string, string>)taskItem2.CloneCustomMetadataEscaped();
            Assert.Equal(3, taskItem2MetadataEscaped.Count);

            foreach (KeyValuePair<string, string> pair in taskItem2MetadataEscaped)
            {
                if (pair.Key.Equals("m"))
                {
                    Assert.Equal("m1", pair.Value);
                }

                if (pair.Key.Equals("m;"))
                {
                    Assert.Equal("m%3b2", pair.Value);
                }

                if (pair.Key.Equals("OriginalItemSpec"))
                {
                    Assert.Equal("esca%20ped%3bitem", pair.Value);
                }
            }
        }

        /// <summary>
        /// Cast to ITaskItem
        /// </summary>
        [Fact]
        public void CastToITaskItemNoMetadata()
        {
            ProjectItemInstance item = GetItemInstance();

            ITaskItem taskItem = (ITaskItem)item;

            Assert.Equal(0 + BuiltInMetadataCount, taskItem.MetadataCount);
            Assert.Equal(0 + BuiltInMetadataCount, taskItem.MetadataNames.Count);
            Assert.Equal(String.Empty, taskItem.GetMetadata("m"));
        }

        /*
         * We must repeat all the evaluation-related tests here,
         * to exercise the path that evaluates directly to instance objects.
         * Although the Evaluator class is shared, its interactions with the two
         * different item classes could be different, and shouldn't be.
         */

        /// <summary>
        /// No metadata, simple case
        /// </summary>
        [Fact]
        public void NoMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.Equal("i", item.ItemType);
            Assert.Equal("i1", item.EvaluatedInclude);
            Assert.False(item.Metadata.GetEnumerator().MoveNext());
            Assert.Equal(0 + BuiltInMetadataCount, Helpers.MakeList(item.MetadataNames).Count);
            Assert.Equal(0 + BuiltInMetadataCount, item.MetadataCount);
        }

        /// <summary>
        /// Read off metadata
        /// </summary>
        [Fact]
        public void ReadMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1</m1>
                                <m2>v2</m2>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            var itemMetadata = Helpers.MakeList(item.Metadata);

            itemMetadata = itemMetadata.OrderBy(pmi => pmi.Name).ToList();

            Assert.Equal(2, itemMetadata.Count);
            Assert.Equal("m1", itemMetadata[0].Name);
            Assert.Equal("m2", itemMetadata[1].Name);
            Assert.Equal("v1", itemMetadata[0].EvaluatedValue);
            Assert.Equal("v2", itemMetadata[1].EvaluatedValue);

            Assert.Equal(itemMetadata[0], item.GetMetadata("m1"));
            Assert.Equal(itemMetadata[1], item.GetMetadata("m2"));
        }

        /// <summary>
        /// Create a new Microsoft.Build.Utilities.TaskItem from the ProjectItemInstance where the ProjectItemInstance
        /// has item definition metadata on it.
        /// 
        /// Verify the Utilities task item gets the expanded metadata from the ItemDefinitionGroup.
        /// </summary>
        [Fact]
        public void InstanceItemToUtilItemIDG()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i>
                                <m0>;x86;</m0>                                
                                <m1>%(FileName).extension</m1>
                                <m2>;%(FileName).extension;</m2>
                                <m3>v1</m3>
                                <m4>%3bx86%3b</m4> 
                            </i>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                            <i Include='foo.proj'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Microsoft.Build.Utilities.TaskItem taskItem = new Microsoft.Build.Utilities.TaskItem(item);

            Assert.Equal(";x86;", taskItem.GetMetadata("m0"));
            Assert.Equal("foo.extension", taskItem.GetMetadata("m1"));
            Assert.Equal(";foo.extension;", taskItem.GetMetadata("m2"));
            Assert.Equal("v1", taskItem.GetMetadata("m3"));
            Assert.Equal(";x86;", taskItem.GetMetadata("m4"));
        }

        /// <summary>
        /// Get metadata values inherited from item definitions
        /// </summary>
        [Fact]
        public void GetMetadataValuesFromDefinition()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                            <i>
                                <m0>v0</m0>
                                <m1>v1</m1>
                            </i>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1b</m1>
                                <m2>v2</m2>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);
            Assert.Equal("v0", item.GetMetadataValue("m0"));
            Assert.Equal("v1b", item.GetMetadataValue("m1"));
            Assert.Equal("v2", item.GetMetadataValue("m2"));

            Assert.Equal(3, Helpers.MakeList(item.Metadata).Count);
            Assert.Equal(3 + BuiltInMetadataCount, Helpers.MakeList(item.MetadataNames).Count);
            Assert.Equal(3 + BuiltInMetadataCount, item.MetadataCount);
        }

        /// <summary>
        /// Exclude against an include with item vectors in it
        /// </summary>
        [Fact]
        public void ExcludeWithIncludeVector()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='a;b;c'>
                            </i>
                        </ItemGroup>

                        <ItemGroup>
                            <i Include='x;y;z;@(i);u;v;w' Exclude='b;y;v'>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            // Should contain a, b, c, x, z, a, c, u, w
            Assert.Equal(9, items.Count);
            AssertEvaluatedIncludes(items, new string[] { "a", "b", "c", "x", "z", "a", "c", "u", "w" });
        }

        /// <summary>
        /// Exclude with item vectors against an include with item vectors in it
        /// </summary>
        [Fact]
        public void ExcludeVectorWithIncludeVector()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='a;b;c'>
                            </i>
                            <j Include='b;y;v' />
                        </ItemGroup>

                        <ItemGroup>
                            <i Include='x;y;z;@(i);u;v;w' Exclude='x;@(j);w'>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            // Should contain a, b, c, z, a, c, u
            Assert.Equal(7, items.Count);
            AssertEvaluatedIncludes(items, new string[] { "a", "b", "c", "z", "a", "c", "u" });
        }

        /// <summary>
        /// Metadata on items can refer to metadata above
        /// </summary>
        [Fact]
        public void MetadataReferringToMetadataAbove()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1</m1>
                                <m2>%(m1);v2;%(m0)</m2>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            var itemMetadata = Helpers.MakeList(item.Metadata);
            Assert.Equal(2, itemMetadata.Count);
            Assert.Equal("v1;v2;", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Built-in metadata should work, too.
        /// NOTE: To work properly, this should batch. This is a temporary "patch" to make it work for now.
        /// It will only give correct results if there is exactly one item in the Include. Otherwise Batching would be needed.
        /// </summary>
        [Fact]
        public void BuiltInMetadataExpression()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m>%(Identity)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.Equal("i1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Qualified built in metadata should work
        /// </summary>
        [Fact]
        public void BuiltInQualifiedMetadataExpression()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m>%(i.Identity)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.Equal("i1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Mis-qualified built in metadata should not work
        /// </summary>
        [Fact]
        public void BuiltInMisqualifiedMetadataExpression()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m>%(j.Identity)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.Equal(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Metadata condition should work correctly with built-in metadata 
        /// </summary>
        [Fact]
        public void BuiltInMetadataInMetadataCondition()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m Condition=""'%(Identity)'=='i1'"">m1</m>
                                <n Condition=""'%(Identity)'=='i2'"">n1</n>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.Equal("m1", item.GetMetadataValue("m"));
            Assert.Equal(String.Empty, item.GetMetadataValue("n"));
        }

        /// <summary>
        /// Metadata on item condition not allowed (currently)
        /// </summary>
        [Fact]
        public void BuiltInMetadataInItemCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' Condition=""'%(Identity)'=='i1'/>
                        </ItemGroup>
                    </Project>
                ";

                GetOneItem(content);
            }
           );
        }
        /// <summary>
        /// Two items should each get their own values for built-in metadata
        /// </summary>
        [Fact]
        public void BuiltInMetadataTwoItems()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1.cpp;" + (NativeMethodsShared.IsWindows ? @"c:\bar\i2.cpp" : "/bar/i2.cpp") + @"'>
                                <m>%(Filename).obj</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal(@"i1.obj", items[0].GetMetadataValue("m"));
            Assert.Equal(@"i2.obj", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Items from another list, but with different metadata
        /// </summary>
        [Fact]
        public void DifferentMetadataItemsFromOtherList()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <h Include='h0'>
                                <m>m1</m>
                            </h>
                            <h Include='h1'/>

                            <i Include='@(h)'>
                                <m>%(m)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal(@"m1", items[0].GetMetadataValue("m"));
            Assert.Equal(String.Empty, items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Items from another list, but with different metadata
        /// </summary>
        [Fact]
        public void DifferentBuiltInMetadataItemsFromOtherList()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <h Include='h0.x'/>
                            <h Include='h1.y'/>

                            <i Include='@(h)'>
                                <m>%(extension)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal(@".x", items[0].GetMetadataValue("m"));
            Assert.Equal(@".y", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Two items coming from a transform
        /// </summary>
        [Fact]
        public void BuiltInMetadataTransformInInclude()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <h Include='h0'/>
                            <h Include='h1'/>

                            <i Include=""@(h->'%(Identity).baz')"">
                                <m>%(Filename)%(Extension).obj</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal(@"h0.baz.obj", items[0].GetMetadataValue("m"));
            Assert.Equal(@"h1.baz.obj", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Transform in the metadata value; no bare metadata involved
        /// </summary>
        [Fact]
        public void BuiltInMetadataTransformInMetadataValue()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <h Include='h0'/>
                            <h Include='h1'/>
                            <i Include='i0'/>
                            <i Include='i1;i2'>
                                <m>@(i);@(h->'%(Filename)')</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal(@"i0;h0;h1", items[1].GetMetadataValue("m"));
            Assert.Equal(@"i0;h0;h1", items[2].GetMetadataValue("m"));
        }

        /// <summary>
        /// Transform in the metadata value; bare metadata involved
        /// </summary>
        [Fact]
        public void BuiltInMetadataTransformInMetadataValueBareMetadataPresent()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <h Include='h0'/>
                            <h Include='h1'/>
                            <i Include='i0.x'/>
                            <i Include='i1.y;i2'>
                                <m>@(i);@(h->'%(Filename)');%(Extension)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal(@"i0.x;h0;h1;.y", items[1].GetMetadataValue("m"));
            Assert.Equal(@"i0.x;h0;h1;", items[2].GetMetadataValue("m"));
        }

        /// <summary>
        /// Metadata on items can refer to item lists
        /// </summary>
        [Fact]
        public void MetadataValueReferringToItems()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <h Include='h0'/>
                            <i Include='i0'/>
                            <i Include='i1'>
                                <m1>@(h);@(i)</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal("h0;i0", items[1].GetMetadataValue("m1"));
        }

        /// <summary>
        /// Metadata on items' conditions can refer to item lists
        /// </summary>
        [Fact]
        public void MetadataConditionReferringToItems()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <h Include='h0'/>
                            <i Include='i0'/>
                            <i Include='i1'>
                                <m1 Condition=""'@(h)'=='h0' and '@(i)'=='i0'"">v1</m1>
                                <m2 Condition=""'@(h)'!='h0' or '@(i)'!='i0'"">v2</m2>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal("v1", items[1].GetMetadataValue("m1"));
            Assert.Equal(String.Empty, items[1].GetMetadataValue("m2"));
        }

        /// <summary>
        /// Metadata on items' conditions can refer to other metadata
        /// </summary>
        [Fact]
        public void MetadataConditionReferringToMetadataOnSameItem()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m0>0</m0>
                                <m1 Condition=""'%(m0)'=='0'"">1</m1>
                                <m2 Condition=""'%(m0)'=='3'"">2</m2>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.Equal("0", items[0].GetMetadataValue("m0"));
            Assert.Equal("1", items[0].GetMetadataValue("m1"));
            Assert.Equal(String.Empty, items[0].GetMetadataValue("m2"));
        }

        [Fact]
        public void UpdateShouldRespectConditions()
        {
            string content = @"
                      <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                          <ItemGroup>
                              <i Include='a;b'>
                                  <m1>m1_contents</m1>
                              </i>
                              <i Update='a' Condition='1 == 1'>
                                  <m1>from_true</m1>
                              </i>
                              <i Update='b' Condition='1 == 0'>
                                  <m1>from_false_on_item</m1>
                              </i>
                              <i Update='b'>
                                  <m1 Condition='1 == 0'>from_false_on_metadata</m1>
                              </i>
                          </ItemGroup>
                      </Project>";

            var items = GetItems(content);

            var expectedInitial = new Dictionary<string, string>
            {
                {"m1", "m1_contents"}
            };

            var expectedUpdateFromTrue = new Dictionary<string, string>
            {
                {"m1", "from_true"}
            };

            AssertItemHasMetadata(expectedUpdateFromTrue, items[0]);
            AssertItemHasMetadata(expectedInitial, items[1]);
        }

        /// <summary>
        /// Gets the first item of type 'i'
        /// </summary>
        private static ProjectItemInstance GetOneItem(string content)
        {
            return GetItems(content)[0];
        }

        /// <summary>
        /// Get all items of type 'i'
        /// </summary>
        private static IList<ProjectItemInstance> GetItems(string content)
        {
            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectInstance project = new ProjectInstance(xml);

            return Helpers.MakeList(project.GetItems("i"));
        }

        /// <summary>
        /// Asserts that the list of items has the specified includes.
        /// </summary>
        private static void AssertEvaluatedIncludes(IList<ProjectItemInstance> items, string[] includes)
        {
            for (int i = 0; i < includes.Length; i++)
            {
                Assert.Equal(includes[i], items[i].EvaluatedInclude);
            }
        }

        /// <summary>
        /// Get a single item instance
        /// </summary>
        private static ProjectItemInstance GetItemInstance()
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();
            ProjectItemInstance item = projectInstance.AddItem("i", "i1");
            return item;
        }

        private static void AssertItemHasMetadata(Dictionary<string, string> expected, ProjectItemInstance item)
        {
            Assert.Equal(expected.Keys.Count, item.DirectMetadataCount);

            foreach (var key in expected.Keys)
            {
                Assert.Equal(expected[key], item.GetMetadataValue(key));
            }
        }
    }
}
