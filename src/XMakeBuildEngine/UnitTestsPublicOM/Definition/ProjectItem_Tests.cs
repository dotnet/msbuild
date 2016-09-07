// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
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
using Microsoft.Build.Shared;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectItem
    /// </summary>
    public class ProjectItem_Tests
    {
        /// <summary>
        /// Project getter
        /// </summary>
        [Fact]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];

            Assert.Equal(true, Object.ReferenceEquals(project, item.Project));
        }

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

            ProjectItem item = GetOneItem(content);

            Assert.NotNull(item.Xml);
            Assert.Equal("i", item.ItemType);
            Assert.Equal("i1", item.EvaluatedInclude);
            Assert.Equal("i1", item.UnevaluatedInclude);
            Assert.Equal(false, item.Metadata.GetEnumerator().MoveNext());
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

            ProjectItem item = GetOneItem(content);

            var itemMetadata = Helpers.MakeList(item.Metadata);
            Assert.Equal(2, itemMetadata.Count);
            Assert.Equal("m1", itemMetadata[0].Name);
            Assert.Equal("m2", itemMetadata[1].Name);
            Assert.Equal("v1", itemMetadata[0].EvaluatedValue);
            Assert.Equal("v2", itemMetadata[1].EvaluatedValue);

            Assert.Equal(itemMetadata[0], item.GetMetadata("m1"));
            Assert.Equal(itemMetadata[1], item.GetMetadata("m2"));
        }

        /// <summary>
        /// Get metadata inherited from item definitions
        /// </summary>
        [Fact]
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

            Assert.Equal(true, Object.ReferenceEquals(m0, idm0));
            Assert.Equal(false, Object.ReferenceEquals(m1, idm1));
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

            ProjectItem item = GetOneItem(content);

            Assert.Equal("v0", item.GetMetadataValue("m0"));
            Assert.Equal("v1b", item.GetMetadataValue("m1"));
            Assert.Equal("v2", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Getting nonexistent metadata should return null
        /// </summary>
        [Fact]
        public void GetNonexistentMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='i0'/>");

            Assert.Equal(null, item.GetMetadata("m0"));
        }

        /// <summary>
        /// Getting value of nonexistent metadata should return String.Empty
        /// </summary>
        [Fact]
        public void GetNonexistentMetadataValue()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='i0'/>");

            Assert.Equal(String.Empty, item.GetMetadataValue("m0"));
        }

        /// <summary>
        /// Attempting to set metadata with an invalid XML name should fail
        /// </summary>
        [Fact]
        public void SetInvalidXmlNameMetadata()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

                item.SetMetadataValue("##invalid##", "x");
            }
           );
        }
        /// <summary>
        /// Attempting to set built-in metadata should fail
        /// </summary>
        [Fact]
        public void SetInvalidBuiltInMetadata()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

                item.SetMetadataValue("FullPath", "x");
            }
           );
        }
        /// <summary>
        /// Attempting to set reserved metadata should fail
        /// </summary>
        [Fact]
        public void SetInvalidReservedMetadata()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

                item.SetMetadataValue("Choose", "x");
            }
           );
        }
        /// <summary>
        /// Metadata enumerator should only return custom metadata
        /// </summary>
        [Fact]
        public void MetadataEnumeratorExcludesBuiltInMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            Assert.Equal(false, item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Read off built-in metadata
        /// </summary>
        [Fact]
        public void BuiltInMetadata()
        {
            ProjectItem item =
                GetOneItemFromFragment(
                    NativeMethodsShared.IsWindows ? @"<i Include='c:\foo\bar.baz'/>" : @"<i Include='/foo/bar.baz'/>");

            // c:\foo\bar.baz - /foo/bar.baz   %(FullPath)         = full path of item
            // c:\ - /                         %(RootDir)          = root directory of item
            // bar                             %(Filename)         = item filename without extension
            // .baz                            %(Extension)        = item filename extension
            // c:\foo\ - /foo/                 %(RelativeDir)      = item directory as given in item-spec
            // foo\ - /foo/                    %(Directory)        = full path of item directory relative to root
            // []                              %(RecursiveDir)     = portion of item path that matched a recursive wildcard
            // c:\foo\bar.baz - /foo/bar.baz   %(Identity)         = item-spec as given
            // []                              %(ModifiedTime)     = last write time of item
            // []                              %(CreatedTime)      = creation time of item
            // []                              %(AccessedTime)     = last access time of item
            Assert.Equal(
                NativeMethodsShared.IsWindows ? @"c:\foo\bar.baz" : "/foo/bar.baz",
                item.GetMetadataValue("FullPath"));
            Assert.Equal(NativeMethodsShared.IsWindows ? @"c:\" : "/", item.GetMetadataValue("RootDir"));
            Assert.Equal(@"bar", item.GetMetadataValue("Filename"));
            Assert.Equal(@".baz", item.GetMetadataValue("Extension"));
            Assert.Equal(NativeMethodsShared.IsWindows ? @"c:\foo\" : "/foo/", item.GetMetadataValue("RelativeDir"));
            Assert.Equal(NativeMethodsShared.IsWindows ? @"foo\" : "foo/", item.GetMetadataValue("Directory"));
            Assert.Equal(String.Empty, item.GetMetadataValue("RecursiveDir"));
            Assert.Equal(
                NativeMethodsShared.IsWindows ? @"c:\foo\bar.baz" : "/foo/bar.baz",
                item.GetMetadataValue("Identity"));
        }

        /// <summary>
        /// Check file-timestamp related metadata
        /// </summary>
        [Fact]
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

                Assert.Equal(info.LastWriteTime.ToString(fileTimeFormat), item.GetMetadataValue("ModifiedTime"));
                Assert.Equal(info.CreationTime.ToString(fileTimeFormat), item.GetMetadataValue("CreatedTime"));
                Assert.Equal(info.LastAccessTime.ToString(fileTimeFormat), item.GetMetadataValue("AccessedTime"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Test RecursiveDir metadata
        /// </summary>
        [Fact]
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

                ProjectItem item =
                    GetOneItemFromFragment(
                        "<i Include='" + directory + (NativeMethodsShared.IsWindows ? @"\**\*'/>" : "/**/*'/>"));

                Assert.Equal(NativeMethodsShared.IsWindows ? @"b\" : "b/", item.GetMetadataValue("RecursiveDir"));
                Assert.Equal("c", item.GetMetadataValue("Filename"));
            }
            finally
            {
                File.Delete(file);
                FileUtilities.DeleteWithoutTrailingBackslash(subdirectory);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Correctly establish the "RecursiveDir" value when the include
        /// is semicolon separated.
        /// (This is what requires that the original include fragment [before wildcard
        /// expansion] is stored in the item.)
        /// </summary>
        [Fact]
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

                IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment("<i Include='i0;" + directory + (NativeMethodsShared.IsWindows ? @"\**\*;i2'/>" : "/**/*;i2'/>"));

                Assert.Equal(3, items.Count);
                Assert.Equal("i0", items[0].EvaluatedInclude);
                Assert.Equal(NativeMethodsShared.IsWindows ? @"b\" : "b/", items[1].GetMetadataValue("RecursiveDir"));
                Assert.Equal("i2", items[2].EvaluatedInclude);
            }
            finally
            {
                File.Delete(file);
                FileUtilities.DeleteWithoutTrailingBackslash(subdirectory);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        /// <summary>
        /// Basic exclude case
        /// </summary>
        [Fact]
        public void Exclude()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment("<i Include='a;b' Exclude='b;c'/>");

            Assert.Equal(1, items.Count);
            Assert.Equal("a", items[0].EvaluatedInclude);
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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            // Should contain a, b, c, x, z, a, c, u, w
            Assert.Equal(9, items.Count);
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c", "x", "z", "a", "c", "u", "w" }, items);
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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            // Should contain a, b, c, z, a, c, u
            Assert.Equal(7, items.Count);
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c", "z", "a", "c", "u" }, items);
        }

        private const string ExcludeWithWildCards = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='{0}' Exclude='{1}'/>
                        </ItemGroup>
                    </Project>
                ";
        [Theory]
        [InlineData(
            ExcludeWithWildCards,
            "a.*",
            "*.1",
            new[] { "a.1", "a.2", "a.1" },
            new[] { "a.2" })]
        [InlineData(
            ExcludeWithWildCards,
            @"**\*.cs",
            @"a\**",
            new[] { "1.cs", @"a\2.cs", @"a\b\3.cs", @"a\b\c\4.cs" },
            new[] { "1.cs" })]
        [InlineData(
            ExcludeWithWildCards,
            @"**\*",
            @"**\b\**",
            new[] { "1.cs", @"a\2.cs", @"a\b\3.cs", @"a\b\c\4.cs" },
            new[] { "1.cs", @"a\2.cs", "build.proj" })]
        [InlineData(ExcludeWithWildCards,
            @"**\*",
            @"**\b\**\*.cs",
            new[] { "1.cs", @"a\2.cs", @"a\b\3.cs", @"a\b\c\4.cs", @"a\b\c\5.txt" },
            new[] { "1.cs", @"a\2.cs", @"a\b\c\5.txt", "build.proj" })]
        [InlineData(
            ExcludeWithWildCards,
            @"src\**\proj\**\*.cs",
            @"src\**\proj\**\none\**\*",
            new[]
            {
                "1.cs",
                @"src\2.cs",
                @"src\a\3.cs",
                @"src\proj\4.cs",
                @"src\proj\a\5.cs",
                @"src\a\proj\6.cs",
                @"src\a\proj\a\7.cs",
                @"src\proj\none\8.cs",
                @"src\proj\a\none\9.cs",
                @"src\proj\a\none\a\10.cs",
                @"src\a\proj\a\none\11.cs",
                @"src\a\proj\a\none\a\12.cs"
            },
            new[]
            {
                @"src\a\proj\6.cs",
                @"src\a\proj\a\7.cs",
                @"src\proj\4.cs",
                @"src\proj\a\5.cs"
            })]
        [InlineData(
            ExcludeWithWildCards,
            @"**\*",
            "foo",
            new[]
            {
                "foo",
                @"a\foo",
                @"a\a\foo",
                @"a\b\foo",
            },
            new string[]
            {
                @"a\a\foo",
                @"a\b\foo",
                @"a\foo",
                "build.proj"
            })]
        public void ExcludeVectorWithWildCards(string projectContents, string includeString, string excludeString, string[] inputFiles, string[] expectedInclude)
        {
            //todo run the test twice with slashes and back-slashes when fixing https://github.com/Microsoft/msbuild/issues/724
            string root = "";
            try
            {
                string[] createdFiles;
                string projectFile;
                var formattedProjectContents = string.Format(projectContents, includeString, excludeString);

                root = Helpers.CreateProjectInTempDirectoryWithFiles(formattedProjectContents, inputFiles, out projectFile, out createdFiles);

                // on Unix, the glob expander returns paths with slashes instead of backslashes, therefore we must normalize the slashes when asserting
                ObjectModelHelpers.AssertItems(expectedInclude, new Project(projectFile).Items.ToList(), normalizeSlashes: true);
            }
            finally
            {
                ObjectModelHelpers.DeleteDirectory(root);
                Directory.Delete(root);
            }
        }

        /// <summary>
        /// Expression like @(x) should clone metadata, but metadata should still point at the original XML objects
        /// </summary>
        [Fact]
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

            Assert.Equal("m2", item1.GetMetadataValue("m"));
            Assert.Equal("m1", item2.GetMetadataValue("m"));

            // Should still point at the same XML items
            Assert.Equal(true, Object.ReferenceEquals(item1.GetMetadata("m").Xml, item2.GetMetadata("m").Xml));
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
        [Fact]
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

            Assert.Equal("m1", item1.GetMetadataValue("m"));
            Assert.Equal("m1", item1b.GetMetadataValue("m"));
            Assert.Equal("m1", item1c.GetMetadataValue("m"));
            Assert.Equal("m1", item2.GetMetadataValue("m"));

            project.ItemDefinitions["i"].SetMetadataValue("m", "m2");

            // All the items will see this change
            Assert.Equal("m2", item1.GetMetadataValue("m"));
            Assert.Equal("m2", item1b.GetMetadataValue("m"));
            Assert.Equal("m2", item1c.GetMetadataValue("m"));
            Assert.Equal("m2", item2.GetMetadataValue("m"));

            // And verify we're not still pointing to the definition metadata objects
            item1.SetMetadataValue("m", "m3");
            item1b.SetMetadataValue("m", "m4");
            item1c.SetMetadataValue("m", "m5");
            item2.SetMetadataValue("m", "m6");

            Assert.Equal("m2", project.ItemDefinitions["i"].GetMetadataValue("m")); // Should not have been affected
        }

        /// <summary>
        /// Expression like @(x) should not clone metadata, for perf. See comment on test above.
        /// </summary>
        [Fact]
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

            Assert.Equal("m1", item1.GetMetadataValue("m"));
            Assert.Equal("m1", item1b.GetMetadataValue("m"));
            Assert.Equal("m1", item2.GetMetadataValue("m"));

            project.ItemDefinitions["i"].SetMetadataValue("m", "m2");

            // The items should all see this change
            Assert.Equal("m2", item1.GetMetadataValue("m"));
            Assert.Equal("m2", item1b.GetMetadataValue("m"));
            Assert.Equal("m2", item2.GetMetadataValue("m"));

            // And verify we're not still pointing to the definition metadata objects
            item1.SetMetadataValue("m", "m3");
            item1b.SetMetadataValue("m", "m4");
            item2.SetMetadataValue("m", "m6");

            Assert.Equal("m2", project.ItemDefinitions["i"].GetMetadataValue("m")); // Should not have been affected
        }

        /// <summary>
        /// Repeated copying of items with item definitions should cause the following order of precedence:
        /// 1) direct metadata on the item
        /// 2) item definition metadata on the very first item in the chain
        /// 3) item definition on the next item, and so on until
        /// 4) item definition metadata on the destination item itself
        /// </summary>
        [Fact]
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

            Assert.Equal("l0", project.GetItems("i").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("i").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("i").First().GetMetadataValue("n"));
            Assert.Equal("", project.GetItems("i").First().GetMetadataValue("o"));
            Assert.Equal("", project.GetItems("i").First().GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("j").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("j").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("j").First().GetMetadataValue("n"));
            Assert.Equal("o2", project.GetItems("j").First().GetMetadataValue("o"));
            Assert.Equal("p2", project.GetItems("j").First().GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("k").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("k").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("k").First().GetMetadataValue("n"));
            Assert.Equal("o2", project.GetItems("k").First().GetMetadataValue("o"));
            Assert.Equal("p4", project.GetItems("k").First().GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("l").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("l").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("l").First().GetMetadataValue("n"));
            Assert.Equal("o2", project.GetItems("l").First().GetMetadataValue("o"));
            Assert.Equal("p4", project.GetItems("l").First().GetMetadataValue("p"));

            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("l"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("m"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("n"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("o"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("m").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("m").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("m").First().GetMetadataValue("n"));
            Assert.Equal("o4", project.GetItems("m").First().GetMetadataValue("o"));
            Assert.Equal("p4", project.GetItems("m").First().GetMetadataValue("p"));

            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("l"));
            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("m"));
            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("n"));
            Assert.Equal("o4", project.GetItems("m").ElementAt(1).GetMetadataValue("o"));
            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("p"));

            // Should still point at the same XML metadata
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.Equal(true, !Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
        }

        /// <summary>
        /// Repeated copying of items with item definitions should cause the following order of precedence:
        /// 1) direct metadata on the item
        /// 2) item definition metadata on the very first item in the chain
        /// 3) item definition on the next item, and so on until
        /// 4) item definition metadata on the destination item itself
        /// </summary>
        [Fact]
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

            Assert.Equal("l0", project.GetItems("i").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("i").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("i").First().GetMetadataValue("n"));
            Assert.Equal("", project.GetItems("i").First().GetMetadataValue("o"));
            Assert.Equal("", project.GetItems("i").First().GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("j").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("j").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("j").First().GetMetadataValue("n"));
            Assert.Equal("o2", project.GetItems("j").First().GetMetadataValue("o"));
            Assert.Equal("p2", project.GetItems("j").First().GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("k").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("k").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("k").First().GetMetadataValue("n"));
            Assert.Equal("o2", project.GetItems("k").First().GetMetadataValue("o"));
            Assert.Equal("p4", project.GetItems("k").First().GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("l").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("l").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("l").First().GetMetadataValue("n"));
            Assert.Equal("o2", project.GetItems("l").First().GetMetadataValue("o"));
            Assert.Equal("p4", project.GetItems("l").First().GetMetadataValue("p"));

            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("l"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("m"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("n"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("o"));
            Assert.Equal("", project.GetItems("l").ElementAt(1).GetMetadataValue("p"));

            Assert.Equal("l0", project.GetItems("m").First().GetMetadataValue("l"));
            Assert.Equal("m1", project.GetItems("m").First().GetMetadataValue("m"));
            Assert.Equal("n1", project.GetItems("m").First().GetMetadataValue("n"));
            Assert.Equal("o4", project.GetItems("m").First().GetMetadataValue("o"));
            Assert.Equal("p4", project.GetItems("m").First().GetMetadataValue("p"));

            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("l"));
            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("m"));
            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("n"));
            Assert.Equal("o4", project.GetItems("m").ElementAt(1).GetMetadataValue("o"));
            Assert.Equal("", project.GetItems("m").ElementAt(1).GetMetadataValue("p"));

            // Should still point at the same XML metadata
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.Equal(true, Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.Equal(true, !Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
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

            ProjectItem item = GetOneItem(content);

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

            ProjectItem item = GetOneItem(content);

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

            ProjectItem item = GetOneItem(content);

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

            ProjectItem item = GetOneItem(content);

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

            ProjectItem item = GetOneItem(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            Assert.Equal("0", items[0].GetMetadataValue("m0"));
            Assert.Equal("1", items[0].GetMetadataValue("m1"));
            Assert.Equal(String.Empty, items[0].GetMetadataValue("m2"));
        }

        /// <summary>
        /// Remove a metadatum
        /// </summary>
        [Fact]
        public void RemoveMetadata()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            item.SetMetadataValue("m", "m1");
            project.ReevaluateIfNecessary();

            bool found = item.RemoveMetadata("m");

            Assert.Equal(true, found);
            Assert.Equal(true, project.IsDirty);
            Assert.Equal(String.Empty, item.GetMetadataValue("m"));
            Assert.Equal(0, Helpers.Count(item.Xml.Metadata));
        }

        /// <summary>
        /// Attempt to remove a metadatum originating from an item definition.
        /// Should fail if it was not overridden.
        /// </summary>
        [Fact]
        public void RemoveItemDefinitionMetadataMasked()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinition("i").AddMetadata("m", "m1");
            xml.AddItem("i", "i1").AddMetadata("m", "m2");
            Project project = new Project(xml);
            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

            bool found = item.RemoveMetadata("m");
            Assert.Equal(true, found);
            Assert.Equal(0, item.DirectMetadataCount);
            Assert.Equal(0, Helpers.Count(item.DirectMetadata));
            Assert.Equal("m1", item.GetMetadataValue("m")); // Now originating from definition!
            Assert.Equal(true, project.IsDirty);
            Assert.Equal(0, item.Xml.Count);
        }

        /// <summary>
        /// Attempt to remove a metadatum originating from an item definition.
        /// Should fail if it was not overridden.
        /// </summary>
        [Fact]
        public void RemoveItemDefinitionMetadataNotMasked()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddItemDefinition("i").AddMetadata("m", "m1");
                xml.AddItem("i", "i1");
                Project project = new Project(xml);
                ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

                item.RemoveMetadata("m"); // Should throw
            }
           );
        }
        /// <summary>
        /// Remove a nonexistent metadatum
        /// </summary>
        [Fact]
        public void RemoveNonexistentMetadata()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            bool found = item.RemoveMetadata("m");

            Assert.Equal(false, found);
            Assert.Equal(false, project.IsDirty);
        }

        /// <summary>
        /// Tests removing built-in metadata.
        /// </summary>
        [Fact]
        public void RemoveBuiltInMetadata()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddItem("i", "i1");
                Project project = new Project(xml);
                ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

                // This should throw
                item.RemoveMetadata("FullPath");
            }
           );
        }
        /// <summary>
        /// Simple rename
        /// </summary>
        [Fact]
        public void Rename()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            // populate built in metadata cache for this item, to verify the cache is cleared out by the rename
            Assert.Equal("i1", item.GetMetadataValue("FileName"));

            item.Rename("i2");

            Assert.Equal("i2", item.Xml.Include);
            Assert.Equal("i2", item.EvaluatedInclude);
            Assert.Equal(true, project.IsDirty);
            Assert.Equal("i2", item.GetMetadataValue("FileName"));
        }

        /// <summary>
        /// Verifies that renaming a ProjectItem whose xml backing is a wildcard doesn't corrupt
        /// the MSBuild evaluation data.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        public void RenameItemInProjectWithWildcards()
        {
            string projectDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
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
                Assert.Equal(Path.GetFileName(sourceFile), projectItem.EvaluatedInclude);
                Assert.Same(projectItem, project.GetItemsByEvaluatedInclude(projectItem.EvaluatedInclude).Single());
                projectItem.Rename(Path.GetFileName(renamedSourceFile));
                File.Move(sourceFile, renamedSourceFile); // repro w/ or w/o this
                project.ReevaluateIfNecessary();
                projectItem = project.Items.Single();
                Assert.Equal(Path.GetFileName(renamedSourceFile), projectItem.EvaluatedInclude);
                Assert.Same(projectItem, project.GetItemsByEvaluatedInclude(projectItem.EvaluatedInclude).Single());
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(projectDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Change item type
        /// </summary>
        [Fact]
        public void ChangeItemType()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.ItemType = "j";

            Assert.Equal("j", item.ItemType);
            Assert.Equal(true, project.IsDirty);
        }

        /// <summary>
        /// Change item type to invalid value
        /// </summary>
        [Fact]
        public void ChangeItemTypeInvalid()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Project project = new Project();
                ProjectItem item = project.AddItem("i", "i1")[0];
                project.ReevaluateIfNecessary();

                item.ItemType = "|";
            }
           );
        }
        /// <summary>
        /// Attempt to rename imported item should fail
        /// </summary>
        [Fact]
        public void RenameImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
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
           );
        }
        /// <summary>
        /// Attempt to set metadata on imported item should fail
        /// </summary>
        [Fact]
        public void SetMetadataImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
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
           );
        }
        /// <summary>
        /// Attempt to remove metadata on imported item should fail
        /// </summary>
        [Fact]
        public void RemoveMetadataImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
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
           );
        }
		
		//  TODO: Should remove tests go in project item tests, project item instance tests, or both?
        [Fact]
        public void Remove()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                "<i Include='a;b' />" +
                "<i Remove='b;c' />"
                );

            Assert.Equal(1, items.Count);
            Assert.Equal("a", items[0].EvaluatedInclude);
        }

        [Fact]
        public void RemoveGlob()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                @"<i Include='a.txt;b.cs;bin\foo.cs' />" +
                @"<i Remove='bin\**' />"
                );

            Assert.Equal(2, items.Count);
            Assert.Equal(@"a.txt;b.cs", string.Join(";", items.Select(i => i.EvaluatedInclude))); ;
        }

        [Fact]
        public void RemoveItemReference()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                @"<i Include='a;b;c;d' />" +
                @"<j Include='b;d' />" +
                @"<i Remove='@(j)' />"
                );

            Assert.Equal(2, items.Count);
            Assert.Equal(@"a;c", string.Join(";", items.Select(i => i.EvaluatedInclude))); ;
        }

        [Fact]
        public void UpdateMetadataShouldAddOrReplace()
        {
            string content = @"<i Include='a;b'>
                                  <m1>m1_contents</m1>
                                  <m2>m2_contents</m2>
                                  <m3>m3_contents</m3>
                              </i>
                              <i Update='a'>
                                  <m1>updated</m1>
                                  <m2></m2>
                                  <m4>added</m4>
                              </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            ObjectModelHelpers.AssertItemHasMetadata(
                new Dictionary<string, string>
                {
                    {"m1", "updated"},
                    {"m2", ""},
                    {"m3", "m3_contents"},
                    {"m4", "added"}
                }
                , items[0]);

            ObjectModelHelpers.AssertItemHasMetadata(
                new Dictionary<string, string>
                {
                    {"m1", "m1_contents"},
                    {"m2", "m2_contents"},
                    {"m3", "m3_contents"}
                }
                , items[1]);
        }

        /// <summary>
        /// Project evaluation is a design time evaluation. Conditions on items are ignored
        /// Conditions on metadata on the other hand appear to be respected on the other hand (don't know why, but that's what the code does).
        /// </summary>
        [Fact]
        public void UpdateShouldNotRespectConditionsOnItems()
        {
            string content = @"<i Include='a;b;c'>
                                  <m1>m1_contents</m1>
                              </i>
                              <i Update='a' Condition='1 == 1'>
                                  <m1>from_true</m1>
                              </i>
                              <i Update='b' Condition='1 == 0'>
                                  <m1>from_false_item</m1>
                              </i>
                              <i Update='c'>
                                  <m1 Condition='1 == 0'>from_false_metadata</m1>
                              </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            var expectedInitial = new Dictionary<string, string>
            {
                {"m1", "m1_contents"}
            };

            var expectedUpdateFromTrue = new Dictionary<string, string>
            {
                {"m1", "from_true"}
            };

            var expectedUpdateFromFalseOnItem = new Dictionary<string, string>
            {
                {"m1", "from_false_item"}
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdateFromTrue, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdateFromFalseOnItem, items[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedInitial, items[2]);
        }

        [Fact]
        public void LastUpdateWins()
        {
            string content = @"<i Include='a'>
                                  <m1>m1_contents</m1>
                              </i>
                              <i Update='a'>
                                  <m1>first</m1>
                              </i>
                              <i Update='a'>
                                  <m1>second</m1>
                              </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            var expectedUpdate = new Dictionary<string, string>
            {
                {"m1", "second"}
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdate, items[0]);
        }

        [Fact]
        public void UpdateWithNoMetadataShouldNotAffectItems()
        {
            string content = @"<i Include='a;b'>
                                  <m1>m1_contents</m1>
                                  <m2>m2_contents</m2>
                                  <m3>m3_contents</m3>
                              </i>
                              <i Update='a'>
                              </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            var expectedMetadata = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"},
                {"m3", "m3_contents"}
            };

            Assert.Equal(2, items.Count);

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[1]);
        }

        [Fact]
        public void UpdateOnNonExistingItemShouldDoNothing()
        {
            string content = @"<i Include='a;b'>
                                  <m1>m1_contents</m1>
                                  <m2>m2_contents</m2>
                              </i>
                              <i Update='c'>
                                  <m1>updated</m1>
                                  <m2></m2>
                                  <m3>added</m3>
                              </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            Assert.Equal(2, items.Count);

            var expectedMetadata = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"},
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[1]);
        }

        [Fact]
        public void UpdateOnEmptyStringShouldThrow()
        {
            string content = @"<i Include='a;b'>
                                  <m1>m1_contents</m1>
                                  <m2>m2_contents</m2>
                              </i>
                              <i Update=''>
                                  <m1>updated</m1>
                                  <m2></m2>
                                  <m3>added</m3>
                              </i>";

            var exception = Assert.Throws<InvalidProjectFileException>(() =>
            {
                IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);
            });

            Assert.Equal("The required attribute \"Update\" is empty or missing from the element <i>.", exception.Message);
        }

        // Complex metadata: metadata references from the same item; item transforms; correct binding of metadata with same name but different item qualifiers
        [Fact]
        public void UpdateShouldSupportComplexMetadata()
        {
            string content = @"
                              <i1 Include='x'>
                                  <m1>%(Identity)</m1>
                              </i1>
                              <i2 Include='a;b'>
                                  <m1>m1_contents</m1>
                                  <m2>m2_contents</m2>
                              </i2>
                              <i2 Update='a;b'>
                                  <m1>%(Identity)</m1>
                                  <m2>%(m1)@(i1 -> '%(m1)')</m2>
                              </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            Assert.Equal(3, items.Count);

            var expectedMetadataX = new Dictionary<string, string>
            {
                {"m1", "x"},
            };

            var expectedMetadataA = new Dictionary<string, string>
            {
                {"m1", "a"},
                {"m2", "ax"},
            };

            var expectedMetadataB = new Dictionary<string, string>
            {
                {"m1", "b"},
                {"m2", "bx"},
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataX, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataB, items[2]);
        }

        [Fact]
        public void UpdateShouldBeAbleToContainGlobs()
        {
            string rootDir = null;

            try
            {
                var content = @"<i Include='*.foo'>
                                    <m1>m1_contents</m1>
                                    <m2>m2_contents</m2>
                                </i>
                                <i Update='*bar*foo'>
                                    <m1>updated</m1>
                                    <m2></m2>
                                    <m3>added</m3>
                                </i>";

                var items = GetItemsFromFragmentWithGlobs(out rootDir, content, "a.foo", "b.foo", "bar1.foo", "bar2.foo");

                Assert.Equal(4, items.Count);

                var expectedInitialMetadata = new Dictionary<string, string>
                {
                    {"m1", "m1_contents"},
                    {"m2", "m2_contents"},
                };

                var expectedUpdatedMetadata = new Dictionary<string, string>
                {
                    {"m1", "updated"},
                    {"m2", ""},
                    {"m3", "added"},
                };

                ObjectModelHelpers.AssertItemHasMetadata(expectedInitialMetadata, items[0]);
                ObjectModelHelpers.AssertItemHasMetadata(expectedInitialMetadata, items[1]);
                ObjectModelHelpers.AssertItemHasMetadata(expectedUpdatedMetadata, items[2]);
                ObjectModelHelpers.AssertItemHasMetadata(expectedUpdatedMetadata, items[3]);
            }
            finally
            {
                ObjectModelHelpers.DeleteDirectory(rootDir);
            }
        }

        [Fact]
        public void UpdateShouldBeAbleToContainItemReferences()
        {
            var content = @"<i1 Include='x;y'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i1>
                            <i1 Update='@(i1)'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i1>
                            <i2 Include='a;y'>
                                <m1>m1_i2_contents</m1>
                                <m2>m2_i2_contents</m2>
                            </i2>
                            <i2 Update='@(i1)'>
                                <m1>m1_i2_updated</m1>
                            </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            Assert.Equal(4, items.Count);

            var expected_i1 = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
            };

            var expected_i2_a = new Dictionary<string, string>
            {
                {"m1", "m1_i2_contents"},
                {"m2", "m2_i2_contents"}
            };

            var expected_i2_y = new Dictionary<string, string>
            {
                {"m1", "m1_i2_updated"},
                {"m2", "m2_i2_contents"}
            };

            ObjectModelHelpers.AssertItemHasMetadata(expected_i1, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expected_i1, items[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expected_i2_a, items[2]);
            ObjectModelHelpers.AssertItemHasMetadata(expected_i2_y, items[3]);
        }

        [Fact]
        public void UpdateShouldBeAbleToContainProperties()
        {
            var content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <PropertyGroup>
                           <P>a</P>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Include='a;b;c'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i>
                            <i Update='$(P);b'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i>
                        </ItemGroup>
                    </Project>"
;

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            Assert.Equal(3, items.Count);

            var expectedInitial = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"}
            };

            var expectedUpdated = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"}
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdated, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdated, items[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedInitial, items[2]);
        }

        [Fact]
        public void UpdateShouldUseCaseInsensitiveMatching()
        {
            var content = @"
                            <i Include='x'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i>
                            <i Update='X'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            var expectedUpdated = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdated, items[0]);
        }

        private static List<ProjectItem> GetItemsFromFragmentWithGlobs(out string rootDir, string itemGroupFragment, params string[] globFiles)
        {
            var formattedProjectContents = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(itemGroupFragment);

            string projectFile;
            string[] createdFiles;
            rootDir = Helpers.CreateProjectInTempDirectoryWithFiles(formattedProjectContents, globFiles, out projectFile, out createdFiles);

            return Helpers.MakeList(new Project(projectFile).GetItems("i"));
        }

        /// <summary>
        /// Get the item of type "i" using the item Xml fragment provided.
        /// If there is more than one, fail. 
        /// </summary>
        private static ProjectItem GetOneItemFromFragment(string fragment)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(fragment);

            Assert.Equal(1, items.Count);
            return items[0];
        }

        /// <summary>
        /// Get the item of type "i" in the project provided.
        /// If there is more than one, fail. 
        /// </summary>
        private static ProjectItem GetOneItem(string content)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            Assert.Equal(1, items.Count);
            return items[0];
        }
    }
}
