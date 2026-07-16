// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectItemInstance public members
    /// </summary>
    [TestClass]
    public class ProjectItemInstance_Tests : IDisposable
    {
        /// <summary>
        /// The number of built-in metadata for items.
        /// </summary>
        public const int BuiltInMetadataCount = 15;
        private Lazy<DummyMappedDrive> _mappedDrive = DummyMappedDriveUtils.GetLazyDummyMappedDrive();


        public void Dispose()
        {
            _mappedDrive.Value?.Dispose();
        }

        internal const string TargetItemWithInclude = @"
            <Project>
                <Target Name='TestTarget'>
                    <ItemGroup>
                        <i Include='{0}'/>
                    </ItemGroup>
                </Target>
            </Project>
            ";

        internal const string TargetItemWithIncludeAndExclude = @"
            <Project>
                <Target Name='TestTarget'>
                    <ItemGroup>
                        <i Include='{0}' Exclude='{1}'/>
                    </ItemGroup>
                </Target>
            </Project>
            ";

        internal const string TargetWithDefinedPropertyAndItemWithInclude = @"
            <Project>
                <PropertyGroup>
                    <{0}>{1}</{0}>
                </PropertyGroup>
                <Target Name='TestTarget'>
                    <ItemGroup>
                        <i Include='{2}' />
                    </ItemGroup>
                </Target>
            </Project>
            ";

        /// <summary>
        /// Basic ProjectItemInstance without metadata
        /// </summary>
        [MSBuildTestMethod]
        public void AccessorsWithoutMetadata()
        {
            ProjectItemInstance item = GetItemInstance();

            Assert.AreEqual("i", item.ItemType);
            Assert.AreEqual("i1", item.EvaluatedInclude);
            Assert.IsFalse(item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Basic ProjectItemInstance with metadata
        /// </summary>
        [MSBuildTestMethod]
        public void AccessorsWithMetadata()
        {
            ProjectItemInstance item = GetItemInstance();

            item.SetMetadata("m1", "v0");
            item.SetMetadata("m1", "v1");
            item.SetMetadata("m2", "v2");

            Assert.AreEqual("m1", item.GetMetadata("m1").Name);
            Assert.AreEqual("m2", item.GetMetadata("m2").Name);
            Assert.AreEqual("v1", item.GetMetadataValue("m1"));
            Assert.AreEqual("v2", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Basic ProjectItemInstance with metadata added using ImportMetadata
        /// </summary>
        [MSBuildTestMethod]
        public void AccessorsWithImportedMetadata()
        {
            ProjectItemInstance item = GetItemInstance();

            ((IMetadataContainer)item).ImportMetadata(new Dictionary<string, string>
            {
                { "m1", "v1" },
                { "m2", "v2" },
            });

            Assert.AreEqual("m1", item.GetMetadata("m1").Name);
            Assert.AreEqual("m2", item.GetMetadata("m2").Name);
            Assert.AreEqual("v1", item.GetMetadataValue("m1"));
            Assert.AreEqual("v2", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// ImportMetadata adds and overwrites metadata, does not delete existing metadata
        /// </summary>
        [MSBuildTestMethod]
        public void ImportMetadataAddsAndOverwrites()
        {
            ProjectItemInstance item = GetItemInstance();

            item.SetMetadata("m1", "v1");
            item.SetMetadata("m2", "v0");

            ((IMetadataContainer)item).ImportMetadata(new Dictionary<string, string>
            {
                { "m2", "v2" },
                { "m3", "v3" },
            });

            // m1 was not deleted, m2 was overwritten, m3 was added
            Assert.AreEqual("v1", item.GetMetadataValue("m1"));
            Assert.AreEqual("v2", item.GetMetadataValue("m2"));
            Assert.AreEqual("v3", item.GetMetadataValue("m3"));
        }

        /// <summary>
        /// Get metadata not present
        /// </summary>
        [MSBuildTestMethod]
        public void GetMissingMetadata()
        {
            ProjectItemInstance item = GetItemInstance();
            Assert.IsNull(item.GetMetadata("X"));
            Assert.AreEqual(String.Empty, item.GetMetadataValue("X"));
        }

        [MSBuildTestMethod]
        public void CopyMetadataToTaskItem()
        {
            ProjectItemInstance fromItem = GetItemInstance();

            fromItem.SetMetadata("m1", "v1");
            fromItem.SetMetadata("m2", "v2");

            ITaskItem toItem = new Utilities.TaskItem();

            ((ITaskItem)fromItem).CopyMetadataTo(toItem);

            Assert.AreEqual("v1", toItem.GetMetadata("m1"));
            Assert.AreEqual("v2", toItem.GetMetadata("m2"));
        }

#if FEATURE_APPDOMAIN
        private sealed class RemoteTaskItemFactory : MarshalByRefObject
        {
            public ITaskItem CreateTaskItem() => new Utilities.TaskItem();
        }

        [MSBuildTestMethod]
        public void CopyMetadataToRemoteTaskItem()
        {
            ProjectItemInstance fromItem = GetItemInstance();

            fromItem.SetMetadata("m1", "v1");
            fromItem.SetMetadata("m2", "v2");

            AppDomain appDomain = null;
            try
            {
                appDomain = AppDomain.CreateDomain("CopyMetadataToRemoteTaskItem", null, AppDomain.CurrentDomain.SetupInformation);
                RemoteTaskItemFactory itemFactory = (RemoteTaskItemFactory)appDomain.CreateInstanceFromAndUnwrap(typeof(RemoteTaskItemFactory).Module.FullyQualifiedName, typeof(RemoteTaskItemFactory).FullName);

                ITaskItem toItem = itemFactory.CreateTaskItem();

                ((ITaskItem)fromItem).CopyMetadataTo(toItem);

                Assert.AreEqual("v1", toItem.GetMetadata("m1"));
                Assert.AreEqual("v2", toItem.GetMetadata("m2"));
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
        }
#endif

        /// <summary>
        /// Set include
        /// </summary>
        [MSBuildTestMethod]
        public void SetInclude()
        {
            ProjectItemInstance item = GetItemInstance();
            item.EvaluatedInclude = "i1b";
            Assert.AreEqual("i1b", item.EvaluatedInclude);
        }

        /// <summary>
        /// Set include to empty string
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidEmptyInclude()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.EvaluatedInclude = String.Empty;
            });
        }
        /// <summary>
        /// Set include to invalid null value
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullInclude()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.EvaluatedInclude = null;
            });
        }
        /// <summary>
        /// Create an item with a metadatum that has a null value
        /// </summary>
        [MSBuildTestMethod]
        public void CreateItemWithNullMetadataValue()
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();

            IDictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("m", null);

            ProjectItemInstance item = projectInstance.AddItem("i", "i1", metadata);
            Assert.AreEqual(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata value
        /// </summary>
        [MSBuildTestMethod]
        public void SetMetadata()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", "m1");
            Assert.AreEqual("m1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata value to empty string
        /// </summary>
        [MSBuildTestMethod]
        public void SetMetadataEmptyString()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", String.Empty);
            Assert.AreEqual(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata value to null value -- this is allowed, but
        /// internally converted to the empty string.
        /// </summary>
        [MSBuildTestMethod]
        public void SetNullMetadataValue()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", null);
            Assert.AreEqual(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Set metadata with invalid empty name
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullMetadataName()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.SetMetadata(null, "m1");
            });
        }
        /// <summary>
        /// Set metadata with invalid empty name
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidEmptyMetadataName()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectItemInstance item = GetItemInstance();
                item.SetMetadata(String.Empty, "m1");
            });
        }
        /// <summary>
        /// Cast to ITaskItem
        /// </summary>
        [MSBuildTestMethod]
        public void CastToITaskItem()
        {
            ProjectItemInstance item = GetItemInstance();
            item.SetMetadata("m", "m1");

            ITaskItem taskItem = (ITaskItem)item;

            Assert.AreEqual(item.EvaluatedInclude, taskItem.ItemSpec);
            Assert.AreEqual(1 + BuiltInMetadataCount, taskItem.MetadataCount);
            Assert.AreEqual(1 + BuiltInMetadataCount, taskItem.MetadataNames.Count);
            Assert.AreEqual("m1", taskItem.GetMetadata("m"));
            taskItem.SetMetadata("m", "m2");
            Assert.AreEqual("m2", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Creates a ProjectItemInstance and casts it to ITaskItem2; makes sure that all escaped information is
        /// maintained correctly.  Also creates a new Microsoft.Build.Utilities.TaskItem from the ProjectItemInstance
        /// and verifies that none of the information is lost.
        /// </summary>
        [MSBuildTestMethod]
        public void ITaskItem2Operations()
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();

            ProjectItemInstance item = projectInstance.AddItem("EscapedItem", "esca%20ped%3bitem");
            item.SetMetadata("m", "m1");
            item.SetMetadata("m;", "m%3b1");
            ITaskItem2 taskItem = (ITaskItem2)item;

            Assert.AreEqual("esca%20ped%3bitem", taskItem.EvaluatedIncludeEscaped);
            Assert.AreEqual("esca ped;item", taskItem.ItemSpec);

            Assert.AreEqual("m;1", taskItem.GetMetadata("m;"));
            Assert.AreEqual("m%3b1", taskItem.GetMetadataValueEscaped("m;"));
            Assert.AreEqual("m1", taskItem.GetMetadataValueEscaped("m"));

            Assert.AreEqual("esca%20ped%3bitem", taskItem.EvaluatedIncludeEscaped);
            Assert.AreEqual("esca ped;item", taskItem.ItemSpec);

            ITaskItem2 taskItem2 = new Microsoft.Build.Utilities.TaskItem(taskItem);

            taskItem2.SetMetadataValueLiteral("m;", "m;2");

            Assert.AreEqual("m%3b2", taskItem2.GetMetadataValueEscaped("m;"));
            Assert.AreEqual("m;2", taskItem2.GetMetadata("m;"));

            IDictionary<string, string> taskItem2Metadata = (IDictionary<string, string>)taskItem2.CloneCustomMetadata();
            Assert.AreEqual(3, taskItem2Metadata.Count);

            foreach (KeyValuePair<string, string> pair in taskItem2Metadata)
            {
                if (pair.Key.Equals("m"))
                {
                    Assert.AreEqual("m1", pair.Value);
                }

                if (pair.Key.Equals("m;"))
                {
                    Assert.AreEqual("m;2", pair.Value);
                }

                if (pair.Key.Equals("OriginalItemSpec"))
                {
                    Assert.AreEqual("esca ped;item", pair.Value);
                }
            }

            IDictionary<string, string> taskItem2MetadataEscaped = (IDictionary<string, string>)taskItem2.CloneCustomMetadataEscaped();
            Assert.AreEqual(3, taskItem2MetadataEscaped.Count);

            foreach (KeyValuePair<string, string> pair in taskItem2MetadataEscaped)
            {
                if (pair.Key.Equals("m"))
                {
                    Assert.AreEqual("m1", pair.Value);
                }

                if (pair.Key.Equals("m;"))
                {
                    Assert.AreEqual("m%3b2", pair.Value);
                }

                if (pair.Key.Equals("OriginalItemSpec"))
                {
                    Assert.AreEqual("esca%20ped%3bitem", pair.Value);
                }
            }
        }

        /// <summary>
        /// Cast to ITaskItem
        /// </summary>
        [MSBuildTestMethod]
        public void CastToITaskItemNoMetadata()
        {
            ProjectItemInstance item = GetItemInstance();

            ITaskItem taskItem = (ITaskItem)item;

            Assert.AreEqual(0 + BuiltInMetadataCount, taskItem.MetadataCount);
            Assert.AreEqual(0 + BuiltInMetadataCount, taskItem.MetadataNames.Count);
            Assert.AreEqual(String.Empty, taskItem.GetMetadata("m"));
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
        [MSBuildTestMethod]
        public void NoMetadata()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.AreEqual("i", item.ItemType);
            Assert.AreEqual("i1", item.EvaluatedInclude);
            Assert.IsFalse(item.Metadata.GetEnumerator().MoveNext());
            Assert.AreEqual(0 + BuiltInMetadataCount, Helpers.MakeList(item.MetadataNames).Count);
            Assert.AreEqual(0 + BuiltInMetadataCount, item.MetadataCount);
        }

        /// <summary>
        /// Read off metadata
        /// </summary>
        [MSBuildTestMethod]
        public void ReadMetadata()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual(2, itemMetadata.Count);
            Assert.AreEqual("m1", itemMetadata[0].Name);
            Assert.AreEqual("m2", itemMetadata[1].Name);
            Assert.AreEqual("v1", itemMetadata[0].EvaluatedValue);
            Assert.AreEqual("v2", itemMetadata[1].EvaluatedValue);

            Assert.AreEqual(itemMetadata[0], item.GetMetadata("m1"));
            Assert.AreEqual(itemMetadata[1], item.GetMetadata("m2"));
        }

        /// <summary>
        /// Create a new Microsoft.Build.Utilities.TaskItem from the ProjectItemInstance where the ProjectItemInstance
        /// has item definition metadata on it.
        ///
        /// Verify the Utilities task item gets the expanded metadata from the ItemDefinitionGroup.
        /// </summary>
        [MSBuildTestMethod]
        public void InstanceItemToUtilItemIDG()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual(";x86;", taskItem.GetMetadata("m0"));
            Assert.AreEqual("foo.extension", taskItem.GetMetadata("m1"));
            Assert.AreEqual(";foo.extension;", taskItem.GetMetadata("m2"));
            Assert.AreEqual("v1", taskItem.GetMetadata("m3"));
            Assert.AreEqual(";x86;", taskItem.GetMetadata("m4"));
        }

        /// <summary>
        /// Get metadata values inherited from item definitions
        /// </summary>
        [MSBuildTestMethod]
        public void GetMetadataValuesFromDefinition()
        {
            string content = @"
                    <Project>
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
            Assert.AreEqual("v0", item.GetMetadataValue("m0"));
            Assert.AreEqual("v1b", item.GetMetadataValue("m1"));
            Assert.AreEqual("v2", item.GetMetadataValue("m2"));

            Assert.AreEqual(3, Helpers.MakeList(item.Metadata).Count);
            Assert.AreEqual(3 + BuiltInMetadataCount, Helpers.MakeList(item.MetadataNames).Count);
            Assert.AreEqual(3 + BuiltInMetadataCount, item.MetadataCount);
        }

        /// <summary>
        /// Exclude against an include with item vectors in it
        /// </summary>
        [MSBuildTestMethod]
        public void ExcludeWithIncludeVector()
        {
            string content = @"
                    <Project>
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
            Assert.AreEqual(9, items.Count);
            AssertEvaluatedIncludes(items, new string[] { "a", "b", "c", "x", "z", "a", "c", "u", "w" });
        }

        /// <summary>
        /// Exclude with item vectors against an include with item vectors in it
        /// </summary>
        [MSBuildTestMethod]
        public void ExcludeVectorWithIncludeVector()
        {
            string content = @"
                    <Project>
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
            Assert.AreEqual(7, items.Count);
            AssertEvaluatedIncludes(items, new string[] { "a", "b", "c", "z", "a", "c", "u" });
        }

        /// <summary>
        /// Metadata on items can refer to metadata above
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataReferringToMetadataAbove()
        {
            string content = @"
                    <Project>
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
            Assert.AreEqual(2, itemMetadata.Count);
            Assert.AreEqual("v1;v2;", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Built-in metadata should work, too.
        /// NOTE: To work properly, this should batch. This is a temporary "patch" to make it work for now.
        /// It will only give correct results if there is exactly one item in the Include. Otherwise Batching would be needed.
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataExpression()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <m>%(Identity)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.AreEqual("i1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Qualified built in metadata should work
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInQualifiedMetadataExpression()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <m>%(i.Identity)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.AreEqual("i1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Mis-qualified built in metadata should not work
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMisqualifiedMetadataExpression()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <m>%(j.Identity)</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.AreEqual(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Metadata condition should work correctly with built-in metadata
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataInMetadataCondition()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <m Condition=""'%(Identity)'=='i1'"">m1</m>
                                <n Condition=""'%(Identity)'=='i2'"">n1</n>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItemInstance item = GetOneItem(content);

            Assert.AreEqual("m1", item.GetMetadataValue("m"));
            Assert.AreEqual(String.Empty, item.GetMetadataValue("n"));
        }

        /// <summary>
        /// Metadata on item condition not allowed (currently)
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataInItemCondition()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' Condition=""'%(Identity)'=='i1'/>
                        </ItemGroup>
                    </Project>
                ";

                GetOneItem(content);
            });
        }
        /// <summary>
        /// Two items should each get their own values for built-in metadata
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataTwoItems()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1.cpp;" + (NativeMethodsShared.IsWindows ? @"c:\bar\i2.cpp" : "/bar/i2.cpp") + @"'>
                                <m>%(Filename).obj</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItemInstance> items = GetItems(content);

            Assert.AreEqual(@"i1.obj", items[0].GetMetadataValue("m"));
            Assert.AreEqual(@"i2.obj", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Items from another list, but with different metadata
        /// </summary>
        [MSBuildTestMethod]
        public void DifferentMetadataItemsFromOtherList()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual(@"m1", items[0].GetMetadataValue("m"));
            Assert.AreEqual(String.Empty, items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Items from another list, but with different metadata
        /// </summary>
        [MSBuildTestMethod]
        public void DifferentBuiltInMetadataItemsFromOtherList()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual(@".x", items[0].GetMetadataValue("m"));
            Assert.AreEqual(@".y", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Two items coming from a transform
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataTransformInInclude()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual(@"h0.baz.obj", items[0].GetMetadataValue("m"));
            Assert.AreEqual(@"h1.baz.obj", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Transform in the metadata value; no bare metadata involved
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataTransformInMetadataValue()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual(@"i0;h0;h1", items[1].GetMetadataValue("m"));
            Assert.AreEqual(@"i0;h0;h1", items[2].GetMetadataValue("m"));
        }

        /// <summary>
        /// Transform in the metadata value; bare metadata involved
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataTransformInMetadataValueBareMetadataPresent()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual(@"i0.x;h0;h1;.y", items[1].GetMetadataValue("m"));
            Assert.AreEqual(@"i0.x;h0;h1;", items[2].GetMetadataValue("m"));
        }

        /// <summary>
        /// Metadata on items can refer to item lists
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataValueReferringToItems()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual("h0;i0", items[1].GetMetadataValue("m1"));
        }

        /// <summary>
        /// Metadata on items' conditions can refer to item lists
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataConditionReferringToItems()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual("v1", items[1].GetMetadataValue("m1"));
            Assert.AreEqual(String.Empty, items[1].GetMetadataValue("m2"));
        }

        /// <summary>
        /// Metadata on items' conditions can refer to other metadata
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataConditionReferringToMetadataOnSameItem()
        {
            string content = @"
                    <Project>
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

            Assert.AreEqual("0", items[0].GetMetadataValue("m0"));
            Assert.AreEqual("1", items[0].GetMetadataValue("m1"));
            Assert.AreEqual(String.Empty, items[0].GetMetadataValue("m2"));
        }

        /// <summary>
        /// Fail build for drive enumerating wildcards that exist in projects on any platform.
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(
            TargetItemWithIncludeAndExclude,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        [DataRow(
            TargetItemWithIncludeAndExclude,
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb",
            @"$(Microsoft_WindowsAzure_EngSys)\**\*")]

        [DataRow(
            TargetItemWithInclude,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*")]

        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            "$(Microsoft_WindowsAzure_EngSys)**",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"\")]
        public void ThrowExceptionUponBuildingProjectWithDriveEnumeration(string content, string include, string exclude = null, string property = null, string propertyValue = null)
        {
            content = (string.IsNullOrEmpty(property) && string.IsNullOrEmpty(propertyValue)) ?
                string.Format(content, include, exclude) :
                string.Format(content, property, propertyValue, include);

            Helpers.CleanContentsAndBuildTargetWithDriveEnumeratingWildcard(
                content,
                "1",
                "TestTarget",
                Helpers.ExpectedBuildResult.FailWithError);
        }

        /// <summary>
        /// Log warning for drive enumerating wildcards that exist in projects on Windows platform.
        /// </summary>
        [WindowsOnlyTheory]
        [DataRow(
            TargetItemWithIncludeAndExclude,
            @"%DRIVE%:$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @"$(Microsoft_WindowsAzure_EngSys)**",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"%DRIVE%:\")]

        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"%DRIVE%:")]
        public void LogWindowsWarningUponBuildingProjectWithDriveEnumeration(string content, string include, string exclude = null, string property = null, string propertyValue = null)
        {
            include = DummyMappedDriveUtils.UpdatePathToMappedDrive(include, _mappedDrive.Value.MappedDriveLetter);
            exclude = DummyMappedDriveUtils.UpdatePathToMappedDrive(exclude, _mappedDrive.Value.MappedDriveLetter);
            propertyValue = DummyMappedDriveUtils.UpdatePathToMappedDrive(propertyValue, _mappedDrive.Value.MappedDriveLetter);
            content = (string.IsNullOrEmpty(property) && string.IsNullOrEmpty(propertyValue)) ?
                string.Format(content, include, exclude) :
                string.Format(content, property, propertyValue, include);

            Helpers.CleanContentsAndBuildTargetWithDriveEnumeratingWildcard(
                content,
                "0",
                "TestTarget",
                Helpers.ExpectedBuildResult.SucceedWithWarning);
        }

        /// <summary>
        /// Log warning for drive enumerating wildcards that exist in projects on Unix platform.
        /// </summary>
        [UnixOnlyTheory]
        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @"$(Microsoft_WindowsAzure_EngSys)**",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"/")]

        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @"$(Microsoft_WindowsAzure_EngSys)*/*.log",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"/*")]
        public void LogUnixWarningUponBuildingProjectWithDriveEnumeration(string content, string include, string exclude = null, string property = null, string propertyValue = null)
        {
            content = (string.IsNullOrEmpty(property) && string.IsNullOrEmpty(propertyValue)) ?
                    string.Format(content, include, exclude) :
                    string.Format(content, property, propertyValue, include);

            Helpers.CleanContentsAndBuildTargetWithDriveEnumeratingWildcard(
                    content,
                    "0",
                    "TestTarget",
                    Helpers.ExpectedBuildResult.SucceedWithWarning);
        }

        /// <summary>
        /// Tests target item evaluation resulting in no build failures.
        /// </summary>
        [WindowsOnlyTheory]
        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @"$(Microsoft_WindowsAzure_EngSys)*.cs",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"c:\*\")]

        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @"\$(Microsoft_WindowsAzure_EngSys)*\*.cs",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"c:")]

        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @":\$(Microsoft_WindowsAzure_EngSys)*\*.log",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"c")]

        [DataRow(
            TargetWithDefinedPropertyAndItemWithInclude,
            @"$(Microsoft_WindowsAzure_EngSys)*\*.log",
            null,
            "Microsoft_WindowsAzure_EngSys",
            @"\")]
        public void NoErrorsAndWarningsUponBuildingProject(string content, string include, string exclude = null, string property = null, string propertyValue = null)
        {
            content = (string.IsNullOrEmpty(property) && string.IsNullOrEmpty(propertyValue)) ?
                    string.Format(content, include, exclude) :
                    string.Format(content, property, propertyValue, include);

            Helpers.CleanContentsAndBuildTargetWithDriveEnumeratingWildcard(
                content,
                "0",
                "TestTarget",
                Helpers.ExpectedBuildResult.SucceedWithNoErrorsAndWarnings);
        }

        [MSBuildTestMethod]
        public void UpdateShouldRespectConditions()
        {
            string content = @"
                      <Project>
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
            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement xml = projectRootElementFromString.Project;
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
                Assert.AreEqual(includes[i], items[i].EvaluatedInclude);
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
            Assert.AreEqual(expected.Keys.Count, item.DirectMetadataCount);

            foreach (var key in expected.Keys)
            {
                Assert.AreEqual(expected[key], item.GetMetadataValue(key));
            }
        }
    }
}
