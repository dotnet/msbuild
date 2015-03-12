//-----------------------------------------------------------------------
// <copyright file="ProjectItem_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for ProjectItem</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectItem
    /// </summary>
    [TestClass]
    public class ProjectItem_Tests
    {
        /// <summary>
        /// Gets or sets the test context, assigned by the MSTest test runner.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Project getter
        /// </summary>
        [TestMethod]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];

            Assert.AreEqual(true, Object.ReferenceEquals(project, item.Project));
        }

        /// <summary>
        /// No metadata, simple case
        /// </summary>
        [TestMethod]
        public void NoMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectItem item = GetOneItem(content);

            Assert.IsNotNull(item.Xml);
            Assert.AreEqual("i", item.ItemType);
            Assert.AreEqual("i1", item.EvaluatedInclude);
            Assert.AreEqual("i1", item.UnevaluatedInclude);
            Assert.AreEqual(false, item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Read off metadata
        /// </summary>
        [TestMethod]
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

            ProjectItem item = GetOneItem(content);

            var itemMetadata = Helpers.MakeList(item.Metadata);
            Assert.AreEqual(2, itemMetadata.Count);
            Assert.AreEqual("m1", itemMetadata[0].Name);
            Assert.AreEqual("m2", itemMetadata[1].Name);
            Assert.AreEqual("v1", itemMetadata[0].EvaluatedValue);
            Assert.AreEqual("v2", itemMetadata[1].EvaluatedValue);

            Assert.AreEqual(itemMetadata[0], item.GetMetadata("m1"));
            Assert.AreEqual(itemMetadata[1], item.GetMetadata("m2"));
        }

        /// <summary>
        /// Get metadata inherited from item definitions
        /// </summary>
        [TestMethod]
        public void GetMetadataObjectsFromDefinition()
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

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));
            ProjectMetadata m0 = item.GetMetadata("m0");
            ProjectMetadata m1 = item.GetMetadata("m1");

            ProjectItemDefinition definition = project.ItemDefinitions["i"];
            ProjectMetadata idm0 = definition.GetMetadata("m0");
            ProjectMetadata idm1 = definition.GetMetadata("m1");

            Assert.AreEqual(true, Object.ReferenceEquals(m0, idm0));
            Assert.AreEqual(false, Object.ReferenceEquals(m1, idm1));
        }

        /// <summary>
        /// Get metadata values inherited from item definitions
        /// </summary>
        [TestMethod]
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

            ProjectItem item = GetOneItem(content);

            Assert.AreEqual("v0", item.GetMetadataValue("m0"));
            Assert.AreEqual("v1b", item.GetMetadataValue("m1"));
            Assert.AreEqual("v2", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Getting nonexistent metadata should return null
        /// </summary>
        [TestMethod]
        public void GetNonexistentMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='i0'/>");

            Assert.AreEqual(null, item.GetMetadata("m0"));
        }

        /// <summary>
        /// Getting value of nonexistent metadata should return String.Empty
        /// </summary>
        [TestMethod]
        public void GetNonexistentMetadataValue()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='i0'/>");

            Assert.AreEqual(String.Empty, item.GetMetadataValue("m0"));
        }

        /// <summary>
        /// Attempting to set metadata with an invalid XML name should fail
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SetInvalidXmlNameMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            item.SetMetadataValue("##invalid##", "x");
        }

        /// <summary>
        /// Attempting to set built-in metadata should fail
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SetInvalidBuiltInMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            item.SetMetadataValue("FullPath", "x");
        }

        /// <summary>
        /// Attempting to set reserved metadata should fail
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetInvalidReservedMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            item.SetMetadataValue("Choose", "x");
        }

        /// <summary>
        /// Metadata enumerator should only return custom metadata
        /// </summary>
        [TestMethod]
        public void MetadataEnumeratorExcludesBuiltInMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            Assert.AreEqual(false, item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Read off built-in metadata
        /// </summary>
        [TestMethod]
        public void BuiltInMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            // c:\foo\bar.baz   %(FullPath)         = full path of item
            // c:\              %(RootDir)          = root directory of item
            // bar              %(Filename)         = item filename without extension
            // .baz              %(Extension)        = item filename extension
            // c:\foo\           %(RelativeDir)      = item directory as given in item-spec
            // foo\              %(Directory)        = full path of item directory relative to root
            // []                %(RecursiveDir)     = portion of item path that matched a recursive wildcard
            // c:\foo\bar.baz    %(Identity)         = item-spec as given
            // []                %(ModifiedTime)     = last write time of item
            // []                %(CreatedTime)      = creation time of item
            // []                %(AccessedTime)     = last access time of item
            Assert.AreEqual(@"c:\foo\bar.baz", item.GetMetadataValue("FullPath"));
            Assert.AreEqual(@"c:\", item.GetMetadataValue("RootDir"));
            Assert.AreEqual(@"bar", item.GetMetadataValue("Filename"));
            Assert.AreEqual(@".baz", item.GetMetadataValue("Extension"));
            Assert.AreEqual(@"c:\foo\", item.GetMetadataValue("RelativeDir"));
            Assert.AreEqual(@"foo\", item.GetMetadataValue("Directory"));
            Assert.AreEqual(String.Empty, item.GetMetadataValue("RecursiveDir"));
            Assert.AreEqual(@"c:\foo\bar.baz", item.GetMetadataValue("Identity"));
        }

        /// <summary>
        /// Check file-timestamp related metadata
        /// </summary>
        [TestMethod]
        public void BuiltInMetadataTimes()
        {
            string path = null;
            string fileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

            try
            {
                path = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                File.WriteAllText(path, String.Empty);
                FileInfo info = new FileInfo(path);

                ProjectItem item = GetOneItemFromFragment(@"<i Include='" + path + "'/>");

                Assert.AreEqual(info.LastWriteTime.ToString(fileTimeFormat), item.GetMetadataValue("ModifiedTime"));
                Assert.AreEqual(info.CreationTime.ToString(fileTimeFormat), item.GetMetadataValue("CreatedTime"));
                Assert.AreEqual(info.LastAccessTime.ToString(fileTimeFormat), item.GetMetadataValue("AccessedTime"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Test RecursiveDir metadata
        /// </summary>
        [TestMethod]
        public void RecursiveDirMetadata()
        {
            string directory = null;
            string subdirectory = null;
            string file = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), "a");
                if (File.Exists(directory))
                {
                    File.Delete(directory);
                }

                subdirectory = Path.Combine(directory, "b");
                if (File.Exists(subdirectory))
                {
                    File.Delete(subdirectory);
                }

                file = Path.Combine(subdirectory, "c");
                Directory.CreateDirectory(subdirectory);

                File.WriteAllText(file, String.Empty);

                ProjectItem item = GetOneItemFromFragment("<i Include='" + directory + @"\**\*'/>");

                Assert.AreEqual(@"b\", item.GetMetadataValue("RecursiveDir"));
                Assert.AreEqual("c", item.GetMetadataValue("Filename"));
            }
            finally
            {
                File.Delete(file);
                Directory.Delete(subdirectory);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Correctly establish the "RecursiveDir" value when the include
        /// is semicolon separated.
        /// (This is what requires that the original include fragment [before wildcard
        /// expansion] is stored in the item.)
        /// </summary>
        [TestMethod]
        public void RecursiveDirWithSemicolonSeparatedInclude()
        {
            string directory = null;
            string subdirectory = null;
            string file = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), "a");
                if (File.Exists(directory))
                {
                    File.Delete(directory);
                }

                subdirectory = Path.Combine(directory, "b");
                if (File.Exists(subdirectory))
                {
                    File.Delete(subdirectory);
                }

                file = Path.Combine(subdirectory, "c");
                Directory.CreateDirectory(subdirectory);

                File.WriteAllText(file, String.Empty);

                IList<ProjectItem> items = GetItemsFromFragment("<i Include='i0;" + directory + @"\**\*;i2'/>");

                Assert.AreEqual(3, items.Count);
                Assert.AreEqual("i0", items[0].EvaluatedInclude);
                Assert.AreEqual(@"b\", items[1].GetMetadataValue("RecursiveDir"));
                Assert.AreEqual("i2", items[2].EvaluatedInclude);
            }
            finally
            {
                File.Delete(file);
                Directory.Delete(subdirectory);
                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Basic exclude case
        /// </summary>
        [TestMethod]
        public void Exclude()
        {
            IList<ProjectItem> items = GetItemsFromFragment("<i Include='a;b' Exclude='b;c'/>");

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("a", items[0].EvaluatedInclude);
        }

        /// <summary>
        /// Exclude against an include with item vectors in it
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            // Should contain a, b, c, x, z, a, c, u, w
            Assert.AreEqual(9, items.Count);
            AssertEvaluatedIncludes(items, new string[] { "a", "b", "c", "x", "z", "a", "c", "u", "w" });
        }

        /// <summary>
        /// Exclude with item vectors against an include with item vectors in it
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            // Should contain a, b, c, z, a, c, u
            Assert.AreEqual(7, items.Count);
            AssertEvaluatedIncludes(items, new string[] { "a", "b", "c", "z", "a", "c", "u" });
        }

        /// <summary>
        /// Include and Exclude containing wildcards
        /// </summary>
        [TestMethod]
        public void Wildcards()
        {
            string directory = null;
            string file1 = null;
            string file2 = null;
            string file3 = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), "ProjectItem_Tests_Wildcards");
                Directory.CreateDirectory(directory);

                file1 = Path.Combine(directory, "a.1");
                file2 = Path.Combine(directory, "a.2");
                file3 = Path.Combine(directory, "b.1");

                File.WriteAllText(file1, String.Empty);
                File.WriteAllText(file2, String.Empty);
                File.WriteAllText(file3, String.Empty);

                IList<ProjectItem> items = GetItemsFromFragment(String.Format(@"<i Include='{0}\a.*' Exclude='{0}\*.1'/>", directory));

                Assert.AreEqual(1, items.Count);
                Assert.AreEqual(String.Format(@"{0}\a.2", directory), items[0].EvaluatedInclude);
            }
            finally
            {
                File.Delete(file1);
                File.Delete(file2);
                File.Delete(file3);

                Directory.Delete(directory);
            }
        }

        /// <summary>
        /// Expression like @(x) should clone metadata, but metadata should still point at the original XML objects
        /// </summary>
        [TestMethod]
        public void CopyFromWithItemListExpressionClonesMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                          <i Include='i1'>
                            <m>m1</m>
                          </i>
                          <j Include='@(i)'/>
                        </ItemGroup>
                    </Project>
                ";

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            project.GetItems("i").First().SetMetadataValue("m", "m2");

            ProjectItem item1 = project.GetItems("i").First();
            ProjectItem item2 = project.GetItems("j").First();

            Assert.AreEqual("m2", item1.GetMetadataValue("m"));
            Assert.AreEqual("m1", item2.GetMetadataValue("m"));

            // Should still point at the same XML items
            Assert.AreEqual(true, Object.ReferenceEquals(item1.GetMetadata("m").Xml, item2.GetMetadata("m").Xml));
        }

        /// <summary>
        /// Expression like @(x) should not clone metadata, even if the item type is different.
        /// It's obvious that it shouldn't clone it if the item type is the same.
        /// If it is different, it doesn't clone it for performance; even if the item definition metadata
        /// changes later (this is design time), the inheritors of that item definition type 
        /// (even those that have subsequently been transformed to a different itemtype) should see
        /// the changes, by design.
        /// Just to make sure we don't change that behavior, we test it here.
        /// </summary>
        [TestMethod]
        public void CopyFromWithItemListExpressionDoesNotCloneDefinitionMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                          <i>
                            <m>m1</m>
                          </i>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                          <i Include='i1'/>
                          <i Include='@(i)'/>
                          <i Include=""@(i->'%(identity)')"" /><!-- this will have two items, so setting metadata will split it -->
                          <j Include='@(i)'/>
                        </ItemGroup>
                    </Project>
                ";

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item1 = project.GetItems("i").First();
            ProjectItem item1b = project.GetItems("i").ElementAt(1);
            ProjectItem item1c = project.GetItems("i").ElementAt(2);
            ProjectItem item2 = project.GetItems("j").First();

            Assert.AreEqual("m1", item1.GetMetadataValue("m"));
            Assert.AreEqual("m1", item1b.GetMetadataValue("m"));
            Assert.AreEqual("m1", item1c.GetMetadataValue("m"));
            Assert.AreEqual("m1", item2.GetMetadataValue("m"));

            project.ItemDefinitions["i"].SetMetadataValue("m", "m2");

            // All the items will see this change
            Assert.AreEqual("m2", item1.GetMetadataValue("m"));
            Assert.AreEqual("m2", item1b.GetMetadataValue("m"));
            Assert.AreEqual("m2", item1c.GetMetadataValue("m"));
            Assert.AreEqual("m2", item2.GetMetadataValue("m"));

            // And verify we're not still pointing to the definition metadata objects
            item1.SetMetadataValue("m", "m3");
            item1b.SetMetadataValue("m", "m4");
            item1c.SetMetadataValue("m", "m5");
            item2.SetMetadataValue("m", "m6");

            Assert.AreEqual("m2", project.ItemDefinitions["i"].GetMetadataValue("m")); // Should not have been affected
        }

        /// <summary>
        /// Expression like @(x) should not clone metadata, for perf. See comment on test above.
        /// </summary>
        [TestMethod]
        public void CopyFromWithItemListExpressionClonesDefinitionMetadata_Variation()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                          <i>
                            <m>m1</m>
                          </i>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                          <i Include='i1'/>
                          <i Include=""@(i->'%(identity)')"" /><!-- this will have one item-->
                          <j Include='@(i)'/>
                        </ItemGroup>
                    </Project>
                ";

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item1 = project.GetItems("i").First();
            ProjectItem item1b = project.GetItems("i").ElementAt(1);
            ProjectItem item2 = project.GetItems("j").First();

            Assert.AreEqual("m1", item1.GetMetadataValue("m"));
            Assert.AreEqual("m1", item1b.GetMetadataValue("m"));
            Assert.AreEqual("m1", item2.GetMetadataValue("m"));

            project.ItemDefinitions["i"].SetMetadataValue("m", "m2");

            // The items should all see this change
            Assert.AreEqual("m2", item1.GetMetadataValue("m"));
            Assert.AreEqual("m2", item1b.GetMetadataValue("m"));
            Assert.AreEqual("m2", item2.GetMetadataValue("m"));

            // And verify we're not still pointing to the definition metadata objects
            item1.SetMetadataValue("m", "m3");
            item1b.SetMetadataValue("m", "m4");
            item2.SetMetadataValue("m", "m6");

            Assert.AreEqual("m2", project.ItemDefinitions["i"].GetMetadataValue("m")); // Should not have been affected
        }
        
        /// <summary>
        /// Repeated copying of items with item definitions should cause the following order of precedence:
        /// 1) direct metadata on the item
        /// 2) item definition metadata on the very first item in the chain
        /// 3) item definition on the next item, and so on until
        /// 4) item definition metadata on the destination item itself
        /// </summary>
        [TestMethod]
        public void CopyWithItemDefinition()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                          <i>
                            <l>l1</l>
                            <m>m1</m>
                            <n>n1</n>
                          </i>
                          <j>
                            <m>m2</m>
                            <o>o2</o>
                            <p>p2</p>
                          </j>
                          <k>
                            <n>n3</n>
                          </k>
                          <l/>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                          <i Include='i1'>
                            <l>l0</l>
                          </i>
                          <j Include='@(i)'/>
                          <k Include='@(j)'>
                            <p>p4</p>
                          </k>
                          <l Include='@(k);l1'/>
                          <m Include='@(l)'>
                            <o>o4</o>
                          </m>
                        </ItemGroup>
                    </Project>
                ";

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.AreEqual("l0", project.GetItems("i").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("i").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("i").First().GetMetadataValue("n"));
            Assert.AreEqual("", project.GetItems("i").First().GetMetadataValue("o"));
            Assert.AreEqual("", project.GetItems("i").First().GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("j").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("j").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("j").First().GetMetadataValue("n"));
            Assert.AreEqual("o2", project.GetItems("j").First().GetMetadataValue("o"));
            Assert.AreEqual("p2", project.GetItems("j").First().GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("k").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("k").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("k").First().GetMetadataValue("n"));
            Assert.AreEqual("o2", project.GetItems("k").First().GetMetadataValue("o"));
            Assert.AreEqual("p4", project.GetItems("k").First().GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("l").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("l").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("l").First().GetMetadataValue("n"));
            Assert.AreEqual("o2", project.GetItems("l").First().GetMetadataValue("o"));
            Assert.AreEqual("p4", project.GetItems("l").First().GetMetadataValue("p"));

            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("l"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("m"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("n"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("o"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("m").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("m").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("m").First().GetMetadataValue("n"));
            Assert.AreEqual("o4", project.GetItems("m").First().GetMetadataValue("o"));
            Assert.AreEqual("p4", project.GetItems("m").First().GetMetadataValue("p"));

            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("l"));
            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("m"));
            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("n"));
            Assert.AreEqual("o4", project.GetItems("m").ElementAt(1).GetMetadataValue("o"));
            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("p"));

            // Should still point at the same XML metadata
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.AreEqual(true, !Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
        }

        /// <summary>
        /// Repeated copying of items with item definitions should cause the following order of precedence:
        /// 1) direct metadata on the item
        /// 2) item definition metadata on the very first item in the chain
        /// 3) item definition on the next item, and so on until
        /// 4) item definition metadata on the destination item itself
        /// </summary>
        [TestMethod]
        public void CopyWithItemDefinition2()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemDefinitionGroup>
                          <i>
                            <l>l1</l>
                            <m>m1</m>
                            <n>n1</n>
                          </i>
                          <j>
                            <m>m2</m>
                            <o>o2</o>
                            <p>p2</p>
                          </j>
                          <k>
                            <n>n3</n>
                          </k>
                          <l/>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                          <i Include='i1'>
                            <l>l0</l>
                          </i>
                          <j Include='@(i)'/>
                          <k Include='@(j)'>
                            <p>p4</p>
                          </k>
                          <l Include='@(k);l1'/>
                          <m Include='@(l)'>
                            <o>o4</o>
                          </m>
                        </ItemGroup>
                    </Project>
                ";

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.AreEqual("l0", project.GetItems("i").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("i").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("i").First().GetMetadataValue("n"));
            Assert.AreEqual("", project.GetItems("i").First().GetMetadataValue("o"));
            Assert.AreEqual("", project.GetItems("i").First().GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("j").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("j").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("j").First().GetMetadataValue("n"));
            Assert.AreEqual("o2", project.GetItems("j").First().GetMetadataValue("o"));
            Assert.AreEqual("p2", project.GetItems("j").First().GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("k").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("k").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("k").First().GetMetadataValue("n"));
            Assert.AreEqual("o2", project.GetItems("k").First().GetMetadataValue("o"));
            Assert.AreEqual("p4", project.GetItems("k").First().GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("l").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("l").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("l").First().GetMetadataValue("n"));
            Assert.AreEqual("o2", project.GetItems("l").First().GetMetadataValue("o"));
            Assert.AreEqual("p4", project.GetItems("l").First().GetMetadataValue("p"));

            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("l"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("m"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("n"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("o"));
            Assert.AreEqual("", project.GetItems("l").ElementAt(1).GetMetadataValue("p"));

            Assert.AreEqual("l0", project.GetItems("m").First().GetMetadataValue("l"));
            Assert.AreEqual("m1", project.GetItems("m").First().GetMetadataValue("m"));
            Assert.AreEqual("n1", project.GetItems("m").First().GetMetadataValue("n"));
            Assert.AreEqual("o4", project.GetItems("m").First().GetMetadataValue("o"));
            Assert.AreEqual("p4", project.GetItems("m").First().GetMetadataValue("p"));

            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("l"));
            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("m"));
            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("n"));
            Assert.AreEqual("o4", project.GetItems("m").ElementAt(1).GetMetadataValue("o"));
            Assert.AreEqual("", project.GetItems("m").ElementAt(1).GetMetadataValue("p"));

            // Should still point at the same XML metadata
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.AreEqual(true, Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.AreEqual(true, !Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
        }

        /// <summary>
        /// Metadata on items can refer to metadata above
        /// </summary>
        [TestMethod]
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

            ProjectItem item = GetOneItem(content);

            var itemMetadata = Helpers.MakeList(item.Metadata);
            Assert.AreEqual(2, itemMetadata.Count);
            Assert.AreEqual("v1;v2;", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Built-in metadata should work, too.
        /// NOTE: To work properly, this should batch. This is a temporary "patch" to make it work for now.
        /// It will only give correct results if there is exactly one item in the Include. Otherwise Batching would be needed.
        /// </summary>
        [TestMethod]
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

            ProjectItem item = GetOneItem(content);

            Assert.AreEqual("i1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Qualified built in metadata should work
        /// </summary>
        [TestMethod]
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

            ProjectItem item = GetOneItem(content);

            Assert.AreEqual("i1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Mis-qualified built in metadata should not work
        /// </summary>
        [TestMethod]
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

            ProjectItem item = GetOneItem(content);

            Assert.AreEqual(String.Empty, item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Metadata condition should work correctly with built-in metadata 
        /// </summary>
        [TestMethod]
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

            ProjectItem item = GetOneItem(content);

            Assert.AreEqual("m1", item.GetMetadataValue("m"));
            Assert.AreEqual(String.Empty, item.GetMetadataValue("n"));
        }

        /// <summary>
        /// Metadata on item condition not allowed (currently)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void BuiltInMetadataInItemCondition()
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

        /// <summary>
        /// Two items should each get their own values for built-in metadata
        /// </summary>
        [TestMethod]
        public void BuiltInMetadataTwoItems()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1.cpp;c:\bar\i2.cpp'>
                                <m>%(Filename).obj</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual(@"i1.obj", items[0].GetMetadataValue("m"));
            Assert.AreEqual(@"i2.obj", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Items from another list, but with different metadata
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual(@"m1", items[0].GetMetadataValue("m"));
            Assert.AreEqual(String.Empty, items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Items from another list, but with different metadata
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual(@".x", items[0].GetMetadataValue("m"));
            Assert.AreEqual(@".y", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Two items coming from a transform
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual(@"h0.baz.obj", items[0].GetMetadataValue("m"));
            Assert.AreEqual(@"h1.baz.obj", items[1].GetMetadataValue("m"));
        }

        /// <summary>
        /// Transform in the metadata value; no bare metadata involved
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual(@"i0;h0;h1", items[1].GetMetadataValue("m"));
            Assert.AreEqual(@"i0;h0;h1", items[2].GetMetadataValue("m"));
        }

        /// <summary>
        /// Transform in the metadata value; bare metadata involved
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual(@"i0.x;h0;h1;.y", items[1].GetMetadataValue("m"));
            Assert.AreEqual(@"i0.x;h0;h1;", items[2].GetMetadataValue("m"));
        }

        /// <summary>
        /// Metadata on items can refer to item lists
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual("h0;i0", items[1].GetMetadataValue("m1"));
        }

        /// <summary>
        /// Metadata on items' conditions can refer to item lists
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual("v1", items[1].GetMetadataValue("m1"));
            Assert.AreEqual(String.Empty, items[1].GetMetadataValue("m2"));
        }

        /// <summary>
        /// Metadata on items' conditions can refer to other metadata
        /// </summary>
        [TestMethod]
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

            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual("0", items[0].GetMetadataValue("m0"));
            Assert.AreEqual("1", items[0].GetMetadataValue("m1"));
            Assert.AreEqual(String.Empty, items[0].GetMetadataValue("m2"));
        }

        /// <summary>
        /// Remove a metadatum
        /// </summary>
        [TestMethod]
        public void RemoveMetadata()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            item.SetMetadataValue("m", "m1");
            project.ReevaluateIfNecessary();

            bool found = item.RemoveMetadata("m");

            Assert.AreEqual(true, found);
            Assert.AreEqual(true, project.IsDirty);
            Assert.AreEqual(String.Empty, item.GetMetadataValue("m"));
            Assert.AreEqual(0, Helpers.Count(item.Xml.Metadata));
        }

        /// <summary>
        /// Attempt to remove a metadatum originating from an item definition.
        /// Should fail if it was not overridden.
        /// </summary>
        [TestMethod]
        public void RemoveItemDefinitionMetadataMasked()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinition("i").AddMetadata("m", "m1");
            xml.AddItem("i", "i1").AddMetadata("m", "m2");
            Project project = new Project(xml);
            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

            bool found = item.RemoveMetadata("m");
            Assert.AreEqual(true, found);
            Assert.AreEqual(0, item.DirectMetadataCount);
            Assert.AreEqual(0, Helpers.Count(item.DirectMetadata));
            Assert.AreEqual("m1", item.GetMetadataValue("m")); // Now originating from definition!
            Assert.AreEqual(true, project.IsDirty);
            Assert.AreEqual(0, item.Xml.Count);
        }

        /// <summary>
        /// Attempt to remove a metadatum originating from an item definition.
        /// Should fail if it was not overridden.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveItemDefinitionMetadataNotMasked()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinition("i").AddMetadata("m", "m1");
            xml.AddItem("i", "i1");
            Project project = new Project(xml);
            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

            item.RemoveMetadata("m"); // Should throw
        }

        /// <summary>
        /// Remove a nonexistent metadatum
        /// </summary>
        [TestMethod]
        public void RemoveNonexistentMetadata()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            bool found = item.RemoveMetadata("m");

            Assert.AreEqual(false, found);
            Assert.AreEqual(false, project.IsDirty);
        }

        /// <summary>
        /// Tests removing built-in metadata.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void RemoveBuiltInMetadata()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItem("i", "i1");
            Project project = new Project(xml);
            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

            // This should throw
            item.RemoveMetadata("FullPath");
        }

        /// <summary>
        /// Simple rename
        /// </summary>
        [TestMethod]
        public void Rename()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            // populate built in metadata cache for this item, to verify the cache is cleared out by the rename
            Assert.AreEqual("i1", item.GetMetadataValue("FileName"));

            item.Rename("i2");

            Assert.AreEqual("i2", item.Xml.Include);
            Assert.AreEqual("i2", item.EvaluatedInclude);
            Assert.AreEqual(true, project.IsDirty);
            Assert.AreEqual("i2", item.GetMetadataValue("FileName"));
        }

        /// <summary>
        /// Verifies that renaming a ProjectItem whose xml backing is a wildcard doesn't corrupt
        /// the MSBuild evaluation data.
        /// </summary>
        [TestMethod]
        public void RenameItemInProjectWithWildcards()
        {
            string projectDirectory = Path.Combine(this.TestContext.TestRunDirectory, Path.GetRandomFileName());
            Directory.CreateDirectory(projectDirectory);
            try
            {
                string sourceFile = Path.Combine(projectDirectory, "a.cs");
                string renamedSourceFile = Path.Combine(projectDirectory, "b.cs");
                File.Create(sourceFile).Dispose();
                var project = new Project();
                project.AddItem("File", "*.cs");
                project.FullPath = Path.Combine(projectDirectory, "test.proj"); // assign a path so the wildcards can lock onto something.
                project.ReevaluateIfNecessary();

                var projectItem = project.Items.Single();
                Assert.AreEqual(Path.GetFileName(sourceFile), projectItem.EvaluatedInclude);
                Assert.AreSame(projectItem, project.GetItemsByEvaluatedInclude(projectItem.EvaluatedInclude).Single());
                projectItem.Rename(Path.GetFileName(renamedSourceFile));
                File.Move(sourceFile, renamedSourceFile); // repro w/ or w/o this
                project.ReevaluateIfNecessary();
                projectItem = project.Items.Single();
                Assert.AreEqual(Path.GetFileName(renamedSourceFile), projectItem.EvaluatedInclude);
                Assert.AreSame(projectItem, project.GetItemsByEvaluatedInclude(projectItem.EvaluatedInclude).Single());
            }
            finally
            {
                Directory.Delete(projectDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Change item type
        /// </summary>
        [TestMethod]
        public void ChangeItemType()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.ItemType = "j";

            Assert.AreEqual("j", item.ItemType);
            Assert.AreEqual(true, project.IsDirty);
        }

        /// <summary>
        /// Change item type to invalid value
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ChangeItemTypeInvalid()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.ItemType = "|";
        }

        /// <summary>
        /// Attempt to rename imported item should fail
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RenameImported()
        {
            string file = null;

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                Project import = new Project();
                import.AddItem("i", "i1");
                import.Save(file);

                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddImport(file);
                Project project = new Project(xml);

                ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

                item.Rename("i2");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Attempt to set metadata on imported item should fail
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetMetadataImported()
        {
            string file = null;

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                Project import = new Project();
                import.AddItem("i", "i1");
                import.Save(file);

                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddImport(file);
                Project project = new Project(xml);

                ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

                item.SetMetadataValue("m", "m0");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Attempt to remove metadata on imported item should fail
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveMetadataImported()
        {
            string file = null;

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                Project import = new Project();
                ProjectItem item = import.AddItem("i", "i1")[0];
                item.SetMetadataValue("m", "m0");
                import.Save(file);

                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddImport(file);
                Project project = new Project(xml);

                item = Helpers.GetFirst(project.GetItems("i"));

                item.RemoveMetadata("m");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Get items of item type "i" with using the item xml fragment passed in
        /// </summary>
        private static IList<ProjectItem> GetItemsFromFragment(string fragment)
        {
            string content = String.Format
                (
                @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            {0}
                        </ItemGroup>
                    </Project>
                ",
                 fragment
                 );

            IList<ProjectItem> items = GetItems(content);
            return items;
        }

        /// <summary>
        /// Get the item of type "i" using the item Xml fragment provided.
        /// If there is more than one, fail. 
        /// </summary>
        private static ProjectItem GetOneItemFromFragment(string fragment)
        {
            IList<ProjectItem> items = GetItemsFromFragment(fragment);

            Assert.AreEqual(1, items.Count);
            return items[0];
        }

        /// <summary>
        /// Get the items of type "i" in the project provided
        /// </summary>
        private static IList<ProjectItem> GetItems(string content)
        {
            ProjectRootElement projectXml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(projectXml);
            IList<ProjectItem> item = Helpers.MakeList(project.GetItems("i"));

            return item;
        }

        /// <summary>
        /// Get the item of type "i" in the project provided.
        /// If there is more than one, fail. 
        /// </summary>
        private static ProjectItem GetOneItem(string content)
        {
            IList<ProjectItem> items = GetItems(content);

            Assert.AreEqual(1, items.Count);
            return items[0];
        }

        /// <summary>
        /// Asserts that the list of items has the specified includes.
        /// </summary>
        private static void AssertEvaluatedIncludes(IList<ProjectItem> items, string[] includes)
        {
            for (int i = 0; i < includes.Length; i++)
            {
                Assert.AreEqual(includes[i], items[i].EvaluatedInclude);
            }
        }
    }
}
