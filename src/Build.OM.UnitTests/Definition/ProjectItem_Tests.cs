// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Engine.UnitTests.Globbing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectItem
    /// </summary>
    [TestClass]
    public class ProjectItem_Tests : IDisposable
    {
        internal const string ItemWithIncludeAndExclude = @"
                    <Project>
                        <ItemGroup>
                            <i Include='{0}' Exclude='{1}'/>
                        </ItemGroup>
                    </Project>
                ";

        internal const string ItemWithIncludeUpdateAndRemove = @"
                    <Project>
                        <ItemGroup>
                            <i Include='{0}'>
                               <m>contents</m>
                            </i>
                            <i Update='{1}'>
                               <m>updated</m>
                            </i>
                            <i Remove='{2}'/>
                        </ItemGroup>
                    </Project>
                ";

        internal const string ImportProjectElement = @"
                    <Project>
                        <Import Project='{0}'/>
                    </Project>
                ";

        protected readonly TestEnvironment _env;
        private Lazy<DummyMappedDrive> _mappedDrive = DummyMappedDriveUtils.GetLazyDummyMappedDrive();

        public ProjectItem_Tests()
        {
            _env = TestEnvironment.Create();
        }

        public void Dispose()
        {
            _env.Dispose();
            _mappedDrive.Value?.Dispose();
        }

        /// <summary>
        /// Project getter
        /// </summary>
        [MSBuildTestMethod]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];

            Assert.IsTrue(Object.ReferenceEquals(project, item.Project));
        }

        /// <summary>
        /// No metadata, simple case
        /// </summary>
        [MSBuildTestMethod]
        public void SingleItemWithNoMetadata()
        {
            string content = @"
                    <Project>
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
            Assert.IsFalse(item.Metadata.GetEnumerator().MoveNext());
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
        [MSBuildTestMethod]
        public void GetMetadataObjectsFromDefinition()
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

            using ProjectFromString projectFromString = new(content);
            Project project = projectFromString.Project;

            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));
            ProjectMetadata m0 = item.GetMetadata("m0");
            ProjectMetadata m1 = item.GetMetadata("m1");

            ProjectItemDefinition definition = project.ItemDefinitions["i"];
            ProjectMetadata idm0 = definition.GetMetadata("m0");
            ProjectMetadata idm1 = definition.GetMetadata("m1");

            Assert.IsTrue(Object.ReferenceEquals(m0, idm0));
            Assert.IsFalse(Object.ReferenceEquals(m1, idm1));
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

            ProjectItem item = GetOneItem(content);

            Assert.AreEqual("v0", item.GetMetadataValue("m0"));
            Assert.AreEqual("v1b", item.GetMetadataValue("m1"));
            Assert.AreEqual("v2", item.GetMetadataValue("m2"));
        }

        /// <summary>
        /// Getting nonexistent metadata should return null
        /// </summary>
        [MSBuildTestMethod]
        public void GetNonexistentMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='i0'/>");

            Assert.IsNull(item.GetMetadata("m0"));
        }

        /// <summary>
        /// Getting value of nonexistent metadata should return String.Empty
        /// </summary>
        [MSBuildTestMethod]
        public void GetNonexistentMetadataValue()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='i0'/>");

            Assert.AreEqual(String.Empty, item.GetMetadataValue("m0"));
        }

        /// <summary>
        /// Attempting to set metadata with an invalid XML name should fail
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidXmlNameMetadata()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

                item.SetMetadataValue("##invalid##", "x");
            });
        }
        /// <summary>
        /// Attempting to set built-in metadata should fail
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidBuiltInMetadata()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

                item.SetMetadataValue("FullPath", "x");
            });
        }
        /// <summary>
        /// Attempting to set reserved metadata should fail
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidReservedMetadata()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

                item.SetMetadataValue("Choose", "x");
            });
        }
        /// <summary>
        /// Metadata enumerator should only return custom metadata
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataEnumeratorExcludesBuiltInMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            Assert.IsFalse(item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Read off built-in metadata
        /// </summary>
        [MSBuildTestMethod]
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
            Assert.AreEqual(
                NativeMethodsShared.IsWindows ? @"c:\foo\bar.baz" : "/foo/bar.baz",
                item.GetMetadataValue("FullPath"));
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"c:\" : "/", item.GetMetadataValue("RootDir"));
            Assert.AreEqual(@"bar", item.GetMetadataValue("Filename"));
            Assert.AreEqual(@".baz", item.GetMetadataValue("Extension"));
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"c:\foo\" : "/foo/", item.GetMetadataValue("RelativeDir"));
            Assert.AreEqual(NativeMethodsShared.IsWindows ? @"foo\" : "foo/", item.GetMetadataValue("Directory"));
            Assert.AreEqual(String.Empty, item.GetMetadataValue("RecursiveDir"));
            Assert.AreEqual(
                NativeMethodsShared.IsWindows ? @"c:\foo\bar.baz" : "/foo/bar.baz",
                item.GetMetadataValue("Identity"));
        }

        /// <summary>
        /// Check file-timestamp related metadata
        /// </summary>
        [MSBuildTestMethod]
        public void BuiltInMetadataTimes()
        {
            string path = null;
            string fileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

            try
            {
                path = FileUtilities.GetTemporaryFileName();
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
        [MSBuildTestMethod]
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

                Assert.AreEqual(NativeMethodsShared.IsWindows ? @"b\" : "b/", item.GetMetadataValue("RecursiveDir"));
                Assert.AreEqual("c", item.GetMetadataValue("Filename"));
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
        [MSBuildTestMethod]
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

                Assert.AreEqual(3, items.Count);
                Assert.AreEqual("i0", items[0].EvaluatedInclude);
                Assert.AreEqual(NativeMethodsShared.IsWindows ? @"b\" : "b/", items[1].GetMetadataValue("RecursiveDir"));
                Assert.AreEqual("i2", items[2].EvaluatedInclude);
            }
            finally
            {
                File.Delete(file);
                FileUtilities.DeleteWithoutTrailingBackslash(subdirectory);
                FileUtilities.DeleteWithoutTrailingBackslash(directory);
            }
        }

        [MSBuildTestMethod]
        [DataRow(@"<i Condition='false' Include='\**\*.cs'/>")]
        [DataRow(@"<i Condition='false' Include='/**/*.cs'/>")]
        [DataRow(@"<i Condition='false' Include='/**\*.cs'/>")]
        [DataRow(@"<i Condition='false' Include='\**/*.cs'/>")]
        public void FullFileSystemScanGlobWithFalseCondition(string itemDefinition)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(itemDefinition, allItems: false, ignoreCondition: true);
            items.ShouldBeEmpty();
        }

        [MSBuildTestMethod]
        [DataRow(@"<i Condition='false' Include='somedir\**\*.cs'/>")]
        [DataRow(@"<i Condition='false' Include='somedir/**/*.cs'/>")]
        [DataRow(@"<i Condition='false' Include='somedir/**\*.cs'/>")]
        [DataRow(@"<i Condition='false' Include='somedir\**/*.cs'/>")]
        public void PartialFileSystemScanGlobWithFalseCondition(string itemDefinition)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFolder directory = env.CreateFolder(createFolder: true);
                TransientTestFile file = env.CreateFile(directory, "a.cs", String.Empty);

                IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(itemDefinition.Replace("somedir", directory.Path), allItems: false, ignoreCondition: true);
                items.ShouldNotBeEmpty();
            }
        }

        /// <summary>
        /// Basic exclude case
        /// </summary>
        [MSBuildTestMethod]
        public void Exclude()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment("<i Include='a;b' Exclude='b;c'/>");

            Assert.ContainsSingle(items);
            Assert.AreEqual("a", items[0].EvaluatedInclude);
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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            // Should contain a, b, c, x, z, a, c, u, w
            Assert.AreEqual(9, items.Count);
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c", "x", "z", "a", "c", "u", "w" }, items);
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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            // Should contain a, b, c, z, a, c, u
            Assert.AreEqual(7, items.Count);
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c", "z", "a", "c", "u" }, items);
        }

        [MSBuildTestMethod]
        // items as strings: escaped includes appear as unescaped
        [DataRow(ItemWithIncludeAndExclude,
            "%61;%62",
            "b",
            new string[0],
            new[] { "a" })]
        // items as strings: escaped include matches non-escaped exclude
        [DataRow(ItemWithIncludeAndExclude,
            "%61",
            "a",
            new string[0],
            new string[0])]
        // items as strings: non-escaped include matches escaped exclude
        [DataRow(ItemWithIncludeAndExclude,
            "a",
            "%61",
            new string[0],
            new string[0])]
        // items as files: non-escaped wildcard include matches escaped non-wildcard character
        [DataRow(ItemWithIncludeAndExclude,
            "a?b",
            "a%40b",
            new[] { "acb", "a@b" },
            new[] { "acb" })]
        // items as files: non-escaped non-wildcard include matches escaped non-wildcard character
        [DataRow(ItemWithIncludeAndExclude,
           "acb;a@b",
           "a%40b",
           new string[0],
           new[] { "acb" })]
        // items as files: escaped wildcard include matches escaped non-wildcard exclude
        [DataRow(ItemWithIncludeAndExclude,
            "a%40*b",
            "a%40bb",
            new[] { "a@b", "a@ab", "a@bb" },
            new[] { "a@ab", "a@b" })]
        // items as files: escaped wildcard include matches escaped wildcard exclude
        [DataRow(ItemWithIncludeAndExclude,
            "a%40*b",
            "a%40?b",
            new[] { "a@b", "a@ab", "a@bb" },
            new[] { "a@b" })]
        // items as files: non-escaped recursive wildcard include matches escaped recursive wildcard exclude
        [DataRow(ItemWithIncludeAndExclude,
           @"**\a*b",
           @"**\a*%78b",
           new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" },
           new[] { "aab", @"dir\abb" })]
        // items as files: include with non-escaped glob does not match exclude with escaped wildcard character.
        // The exclude is treated as a literal, not a glob, and therefore should not match the input files
        [DataRow(ItemWithIncludeAndExclude,
            @"**\a*b",
            @"**\a%2Axb",
            new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" },
            new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" })]
        public void IncludeExcludeWithEscapedCharacters(string projectContents, string includeString, string excludeString, string[] inputFiles, string[] expectedInclude)
        {
            TestIncludeExcludeWithDifferentSlashes(projectContents, includeString, excludeString, inputFiles, expectedInclude);
        }

        [MSBuildTestMethod]
        // items as strings: include with both escaped and unescaped glob should be treated as literal and therefore not match against files as a glob
        [DataRow(ItemWithIncludeAndExclude,
            @"**\a%2Axb",
            @"foo",
            new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" },
            new[] { @"**\a*xb" })]
        // Include with both escaped and unescaped glob does not match exclude with escaped wildcard character which has a different slash orientation
        // The presence of the escaped and unescaped glob should make things behave as strings-which-are-not-paths and not as strings-which-are-paths
        [DataRow(ItemWithIncludeAndExclude,
            @"**\a%2Axb",
            @"**/a%2Axb",
            new string[0],
            new[] { @"**\a*xb" })]
        // Slashes are not normalized when contents is not a path
        [DataRow(ItemWithIncludeAndExclude,
            @"a/b/foo::||bar;a/b/foo::||bar/;a/b/foo::||bar\;a/b\foo::||bar",
            @"a/b/foo::||bar",
            new string[0],
            new[] { "a/b/foo::||bar/", @"a/b/foo::||bar\", @"a/b\foo::||bar" })]
        public void IncludeExcludeWithNonPathContents(string projectContents, string includeString, string excludeString, string[] inputFiles, string[] expectedInclude)
        {
            TestIncludeExclude(projectContents, inputFiles, expectedInclude, includeString, excludeString, normalizeSlashes: false);
        }

        public static IEnumerable<object[]> IncludesAndExcludesWithWildcardsTestData => GlobbingTestData.IncludesAndExcludesWithWildcardsTestData;

        [MSBuildTestMethod]
        [DynamicData(nameof(IncludesAndExcludesWithWildcardsTestData))]
        public void ExcludeVectorWithWildCards(string includeString, string excludeString, string[] inputFiles, string[] expectedInclude, bool makeExpectedIncludeAbsolute)
        {
            TestIncludeExcludeWithDifferentSlashes(ItemWithIncludeAndExclude, includeString, excludeString, inputFiles, expectedInclude, makeExpectedIncludeAbsolute);
        }

        [MSBuildTestMethod]
        [DataRow(ItemWithIncludeAndExclude,
            @"**\*",
            @"excludes\**.*",
            new[]
            {
                @"a.cs",
                @"excludes\b.cs",
                @"excludes\subdir\c.cs",
            },
            new[]
            {
                @"a.cs",
                "build.proj",
                @"excludes\b.cs",
                @"excludes\subdir\c.cs",
            })]
        [DataRow(ItemWithIncludeAndExclude,
            @"**\*",
            @"excludes\**..\*",
            new[]
            {
                @"a.cs",
                @"excludes\b.cs",
                @"excludes\subdir\c.cs",
            },
            new[]
            {
                @"a.cs",
                "build.proj",
                @"excludes\b.cs",
                @"excludes\subdir\c.cs",
            })]
        [DataRow(ItemWithIncludeAndExclude,
            @"**\*",
            @"**.*",
            new[]
            {
                @"a.cs",
                @"excludes\b.cs",
                @"excludes\subdir\c.cs",
            },
            new[]
            {
                @"a.cs",
                "build.proj",
                @"excludes\b.cs",
                @"excludes\subdir\c.cs",
            })]
        [DataRow(ItemWithIncludeAndExclude,
            "*;**a",
            "**a",
            new[]
            {
                "a",
            },
            new[]
            {
                "a",
                "build.proj"
            })]
        [DataRow(ItemWithIncludeAndExclude,
            @"**1;**2",
            @"**1",
            new[]
            {
                @"1",
                @"2",
                @"excludes\1",
                @"excludes\2",
                @"excludes\subdir\1",
                @"excludes\subdir\2",
            },
            new[]
            {
                "**2"
            })]
        [DataRow(ItemWithIncludeAndExclude,
            @":||;||:",
            @"||:",
            new string[0],
            new[]
            {
                ":||"
            })]
        public void ExcludeAndIncludeConsideredAsLiteralsWhenFilespecIsIllegal(string projectContents, string includeString, string excludeString, string[] inputFiles, string[] expectedInclude)
        {
            TestIncludeExclude(projectContents, inputFiles, expectedInclude, includeString, excludeString, normalizeSlashes: true);
        }

        [WindowsOnlyTheory]
        [DataRow(ItemWithIncludeAndExclude,
            @"src/**/*.cs",
            new[]
            {
                @"src/a.cs",
                @"src/a/b/b.cs",
            },
            new[]
            {
                @"src/a.cs",
                @"src/a/b/b.cs",
            })]
        [DataRow(ItemWithIncludeAndExclude,
            @"src/test/**/*.cs",
            new[]
            {
                @"src/test/a.cs",
                @"src/test/a/b/c.cs",
            },
            new[]
            {
                @"src/test/a.cs",
                @"src/test/a/b/c.cs",
            })]
        [DataRow(ItemWithIncludeAndExclude,
            @"src/test/**/a/b/**/*.cs",
            new[]
            {
                @"src/test/dir/a/b/a.cs",
                @"src/test/dir/a/b/c/a.cs",
            },
            new[]
            {
                @"src/test/dir/a/b/a.cs",
                @"src/test/dir/a/b/c/a.cs",
            })]
        public void IncludeWithWildcardShouldNotPreserveUserSlashesInFixedDirectoryPart(string projectContents, string includeString, string[] inputFiles, string[] expectedInclude)
        {
            Func<string, char, string> setSlashes = (s, c) => s.Replace('/', c).Replace('\\', c);

            // set the include string slashes to the opposite orientation relative to the OS default slash
            if (NativeMethodsShared.IsWindows)
            {
                includeString = setSlashes(includeString, '/');
            }
            else
            {
                includeString = setSlashes(includeString, '\\');
            }

            // all the slashes in the expected items should be platform specific
            expectedInclude = expectedInclude.Select(p => setSlashes(p, Path.DirectorySeparatorChar)).ToArray();

            TestIncludeExclude(projectContents, inputFiles, expectedInclude, includeString, "");
        }

        /// <summary>
        /// Project getter that renames an item to a drive enumerating wildcard that results in an exception.
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"\**\*.log")]
        [DataRow(@"$(empty)\**\*.log")]
        [DataRow(@"\$(empty)**\*.log")]
        [DataRow(@"\*$(empty)*\*.log")]
        public void ProjectGetterResultsInDriveEnumerationException(string unevaluatedInclude)
        {
            using (var env = TestEnvironment.Create())
            {
                try
                {
                    // Setup
                    Helpers.ResetStateForDriveEnumeratingWildcardTests(env, "1");
                    Project project = new Project();

                    // Add item and verify
                    Should.Throw<InvalidProjectFileException>(() => { _ = project.AddItem("i", unevaluatedInclude); });
                }
                finally
                {
                    ChangeWaves.ResetStateForTests();
                }
            }
        }

        /// <summary>
        /// Project getter that renames an item to a drive enumerating wildcard that results in a logged warning.
        /// </summary>
        [WindowsOnlyTheory]
        [DataRow(@"%DRIVE%:\**\*.log")]
        [DataRow(@"%DRIVE%:$(empty)\**\*.log")]
        [DataRow(@"%DRIVE%:\**")]
        [DataRow(@"%DRIVE%:\\**")]
        [DataRow(@"%DRIVE%:\\\\\\\\**")]
        [DataRow(@"%DRIVE%:\**\*.cs")]
        public void ProjectGetterResultsInWindowsDriveEnumerationWarning(string unevaluatedInclude)
        {
            unevaluatedInclude = DummyMappedDriveUtils.UpdatePathToMappedDrive(unevaluatedInclude, _mappedDrive.Value.MappedDriveLetter);
            ProjectGetterResultsInDriveEnumerationWarning(unevaluatedInclude);
        }

        [UnixOnlyTheory]
        [DataRow(@"/**/*.log")]
        [DataRow(@"$(empty)/**/*.log")]
        [DataRow(@"/$(empty)**/*.log")]
        [DataRow(@"/*$(empty)*/*.log")]
        public void ProjectGetterResultsInUnixDriveEnumerationWarning(string unevaluatedInclude)
        {
            ProjectGetterResultsInDriveEnumerationWarning(unevaluatedInclude);
        }

        private static void ProjectGetterResultsInDriveEnumerationWarning(string unevaluatedInclude)
        {
            using (var env = TestEnvironment.Create())
            {
                try
                {
                    // Reset state
                    Helpers.ResetStateForDriveEnumeratingWildcardTests(env, "0");

                    // Setup
                    using ProjectCollection projectCollection = new ProjectCollection();
                    MockLogger collectionLogger = new MockLogger();
                    projectCollection.RegisterLogger(collectionLogger);
                    Project project = new Project(projectCollection);

                    // Add item
                    _ = project.AddItem("i", unevaluatedInclude);

                    // Verify
                    collectionLogger.WarningCount.ShouldBe(1);
                    collectionLogger.AssertLogContains("MSB5029");
                    projectCollection.UnregisterAllLoggers();
                }
                finally
                {
                    ChangeWaves.ResetStateForTests();
                }
            }
        }

        /// <summary>
        /// Project instance created from a file that contains a drive enumerating wildcard results in a thrown exception.
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(
            ImportProjectElement,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            null)]

        // LazyItem.IncludeOperation
        [DataRow(
            ItemWithIncludeAndExclude,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        // LazyItem.IncludeOperation for Exclude
        [DataRow(
            ItemWithIncludeAndExclude,
            @"$(EmptyProperty)\*.cs",
            @"$(Microsoft_WindowsAzure_EngSys)\**")]
        public void ThrowExceptionUponProjectInstanceCreationFromDriveEnumeratingContent(string content, string placeHolder, string excludePlaceHolder = null)
        {
            content = string.Format(content, placeHolder, excludePlaceHolder);
            CleanContentsAndCreateProjectInstanceFromFileWithDriveEnumeratingWildcard(content, true);
        }

        /// <summary>
        /// Project instance created from a file that contains a drive enumerating wildcard results in a logged warning on the Windows platform.
        /// </summary>
        [WindowsOnlyTheory]
        [DataRow(
            ImportProjectElement,
            @"%DRIVE%:\**\*.targets",
            null)]

        // LazyItem.IncludeOperation
        [DataRow(
            ItemWithIncludeAndExclude,
            @"%DRIVE%:$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        // LazyItem.IncludeOperation for Exclude
        [DataRow(
            ItemWithIncludeAndExclude,
            @"$(EmptyProperty)\*.cs",
            @"%DRIVE%:\$(Microsoft_WindowsAzure_EngSys)**")]
        public void LogWindowsWarningUponProjectInstanceCreationFromDriveEnumeratingContent(string content, string placeHolder, string excludePlaceHolder = null)
        {
            placeHolder = DummyMappedDriveUtils.UpdatePathToMappedDrive(placeHolder, _mappedDrive.Value.MappedDriveLetter);
            excludePlaceHolder = DummyMappedDriveUtils.UpdatePathToMappedDrive(excludePlaceHolder, _mappedDrive.Value.MappedDriveLetter);
            content = string.Format(content, placeHolder, excludePlaceHolder);
            CleanContentsAndCreateProjectInstanceFromFileWithDriveEnumeratingWildcard(content, false);
        }

        [UnixOnlyTheory]
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/8373")]
        [DataRow(
            ImportProjectElement,
            @"\**\*.targets",
            null)]

        // LazyItem.IncludeOperation
        [DataRow(
            ItemWithIncludeAndExclude,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        // LazyItem.IncludeOperation for Exclude
        [DataRow(
            ItemWithIncludeAndExclude,
            @"$(EmptyProperty)\*.cs",
            @"$(Microsoft_WindowsAzure_EngSys)\**")]
        public void LogWarningUponProjectInstanceCreationFromDriveEnumeratingContent(string content, string placeHolder, string excludePlaceHolder = null)
        {
            content = string.Format(content, placeHolder, excludePlaceHolder);
            CleanContentsAndCreateProjectInstanceFromFileWithDriveEnumeratingWildcard(content, false);
        }

        private static void CleanContentsAndCreateProjectInstanceFromFileWithDriveEnumeratingWildcard(string content, bool throwException)
        {
            using (var env = TestEnvironment.Create())
            {
                // Clean file contents by replacing single quotes with double quotes, etc.
                content = ObjectModelHelpers.CleanupFileContents(content);
                var testProject = env.CreateTestProjectWithFiles(content.Cleanup());

                // Setup and create project instance from file
                CreateProjectInstanceFromFileWithDriveEnumeratingWildcard(env, testProject.ProjectFile, throwException);
            }
        }

        private static void CreateProjectInstanceFromFileWithDriveEnumeratingWildcard(TestEnvironment env, string testProjectFile, bool throwException)
        {
            try
            {
                // Reset state
                Helpers.ResetStateForDriveEnumeratingWildcardTests(env, throwException ? "1" : "0");

                if (throwException)
                {
                    // Verify
                    Should.Throw<InvalidProjectFileException>(() => { ProjectInstance.FromFile(testProjectFile, new ProjectOptions()); });
                }
                else
                {
                    // Setup
                    MockLogger collectionLogger = new MockLogger();
                    ProjectOptions options = new ProjectOptions();
                    options.ProjectCollection = new ProjectCollection();
                    options.ProjectCollection.RegisterLogger(collectionLogger);

                    // Action
                    ProjectInstance.FromFile(testProjectFile, options);

                    // Verify
                    collectionLogger.WarningCount.ShouldBe(1);
                    collectionLogger.AssertLogContains("MSB5029");
                    collectionLogger.AssertLogContains(testProjectFile);
                    options.ProjectCollection.UnregisterAllLoggers();
                }
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }

        private static void TestIncludeExcludeWithDifferentSlashes(string projectContents, string includeString, string excludeString, string[] inputFiles, string[] expectedInclude, bool makeExpectedIncludeAbsolute = false)
        {
            Action<string, string> runTest = (include, exclude) =>
            {
                TestIncludeExclude(projectContents, inputFiles, expectedInclude, include, exclude, normalizeSlashes: true, makeExpectedIncludeAbsolute: makeExpectedIncludeAbsolute);
            };

            var includeWithForwardSlash = Helpers.ToForwardSlash(includeString);
            var excludeWithForwardSlash = Helpers.ToForwardSlash(excludeString);

            runTest(includeString, excludeString);
            runTest(includeWithForwardSlash, excludeWithForwardSlash);
            runTest(includeString, excludeWithForwardSlash);
            runTest(includeWithForwardSlash, excludeString);
        }

        private static void TestIncludeExclude(string projectContents, string[] inputFiles, string[] expectedInclude, string include, string exclude, bool normalizeSlashes = false, bool makeExpectedIncludeAbsolute = false)
        {
            var formattedProjectContents = string.Format(projectContents, include, exclude);
            ObjectModelHelpers.AssertItemEvaluationFromProject(formattedProjectContents, inputFiles, expectedInclude, expectedMetadataPerItem: null, normalizeSlashes: normalizeSlashes, makeExpectedIncludeAbsolute: makeExpectedIncludeAbsolute);
        }

        [MSBuildTestMethod]
        // exclude matches include; file is next to project file
        [DataRow(ItemWithIncludeAndExclude,
            @"a", // include item
            @"", // path relative from projectFile. Empty string if current directory

            @"a", // exclude item
            "", // path relative from projectFile. Empty string if current directory

            new[] // files relative to this test's root directory. The project is one level deeper than the root.
            {
                @"project\a",
            },
            false) // whether the include survives the exclude (true) or not (false)
            ]
        // exclude matches include; file is below the project file
        [DataRow(ItemWithIncludeAndExclude,
            @"a",
            @"dir",

            @"a",
            "dir",

            new[]
            {
                @"project\dir\a",
            },
            false)]
        // exclude matches include; file is above the project file
        [DataRow(ItemWithIncludeAndExclude,
            @"a",
            @"..",

            @"a",
            "..",

            new[]
            {
                @"a",
            },
            false)]
        // exclude does not match include; file is next to project file; exclude points above the project file
        [DataRow(ItemWithIncludeAndExclude,
            "a",
            "",

            "a",
            "..",

            new[]
            {
                "a",
            },
            true)]
        // exclude does not match include; file is below the project file; exclude points next to the project file
        [DataRow(ItemWithIncludeAndExclude,
            "a",
            "dir",

            "a",
            "",

            new[]
            {
                @"project\dir\a",
            },
            true)]
        // exclude does not match include; file is above the project file; exclude points next to the project file
        [DataRow(ItemWithIncludeAndExclude,
            "a",
            "..",

            "a",
            "",

            new[]
            {
                "a",
            },
            true)]
        public void IncludeAndExcludeWorkWithRelativeAndAbsolutePaths(
            string projectContents,
            string includeItem,
            string includeRelativePath,
            string excludeItem,
            string excludeRelativePath,
            string[] inputFiles,
            bool includeSurvivesExclude)
        {
            Func<bool, string, string, string, string> adjustFilePath = (isAbsolute, testRoot, relativeFragmentFromRootToFile, file) =>
                isAbsolute
                    ? Path.GetFullPath(Path.Combine(testRoot, relativeFragmentFromRootToFile, file))
                    : Path.Combine(relativeFragmentFromRootToFile, file);

            Action<bool, bool> runTest = (includeIsAbsolute, excludeIsAbsolute) =>
            {
                using (var env = TestEnvironment.Create())
                {
                    var projectFile = env
                        .CreateTestProjectWithFiles(projectContents, inputFiles, "project")
                        .ProjectFile;

                    var projectFileDir = Path.GetDirectoryName(projectFile);

                    var include = adjustFilePath(includeIsAbsolute, projectFileDir, includeRelativePath, includeItem);
                    var exclude = adjustFilePath(excludeIsAbsolute, projectFileDir, excludeRelativePath, excludeItem);

                    // includes and exclude may be absolute, so we can only format the project after we have the test directory paths
                    var formattedProject = string.Format(projectContents, include, exclude);
                    File.WriteAllText(projectFile, formattedProject);

                    var expectedInclude = includeSurvivesExclude ? new[] { include } : Array.Empty<string>();

                    ObjectModelHelpers.AssertItems(expectedInclude, new Project(projectFile).Items.ToList());
                }
            };

            runTest(true, false);
            runTest(false, true);
            runTest(true, true);
        }

        [MSBuildTestMethod]
        // exclude globbing cone at project level;
        [DataRow(
            "../a.cs;b.cs", // include string
            "**/*.cs", // exclude string
            new[] { "a.cs", "ProjectDir/b.cs" }, // files to create relative to the test root dir
            "ProjectDir", // relative path from test root to project
            new[] { "../a.cs" }) // expected items
            ]
        // exclude globbing cone below project level;
        [DataRow(
            "a.cs;a/b.cs",
            "a/**/*.cs",
            new[] { "a.cs", "a/b.cs" },
            "",
            new[] { "a.cs" })]
        // exclude globbing above project level;
        [DataRow(
            "a.cs;../b.cs;../../c.cs",
            "../**/*.cs",
            new[] { "a/ProjectDir/a.cs", "a/b.cs", "c.cs" },
            "a/ProjectDir",
            new[] { "../../c.cs" })]
        public void ExcludeWithMissmatchingGlobCones(string includeString, string excludeString, string[] files, string relativePathFromRootToProject, string[] expectedInclude)
        {
            var projectContents = string.Format(ItemWithIncludeAndExclude, includeString, excludeString);

            using (var env = TestEnvironment.Create())
            using (var projectCollection = new ProjectCollection())
            {
                var testFiles = env.CreateTestProjectWithFiles(projectContents, files, relativePathFromRootToProject);
                ObjectModelHelpers.AssertItems(expectedInclude, new Project(testFiles.ProjectFile, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, projectCollection).Items.ToList());
            }
        }

        [MSBuildTestMethod]
        [Ignore("https://github.com/dotnet/msbuild/issues/1576")]
        [DataRow(
            "../**/*.cs", // include string
            "a.cs", // exclude string
            new[] { "ProjectDir/a.cs", "b.cs" }, // files to create relative to the test root dir
            "ProjectDir", // relative path from test root to project
            new[] { "../b.cs" }) // expected items
            ]
        public void ExcludingRelativeItemToCurrentDirectoryShouldWorkWithAboveTheConeIncludes(string includeString, string excludeString, string[] files, string relativePathFromRootToProject, string[] expectedInclude)
        {
            var projectContents = string.Format(ItemWithIncludeAndExclude, includeString, excludeString);

            using (var env = TestEnvironment.Create())
            using (var projectCollection = new ProjectCollection())
            {
                var testFiles = env.CreateTestProjectWithFiles(projectContents, files, relativePathFromRootToProject);
                ObjectModelHelpers.AssertItems(expectedInclude, new Project(testFiles.ProjectFile, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, projectCollection).Items.ToList());
            }
        }

        /// <summary>
        /// Expression like @(x) should clone metadata, but metadata should still point at the original XML objects
        /// </summary>
        [MSBuildTestMethod]
        public void CopyFromWithItemListExpressionClonesMetadata()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                          <i Include='i1'>
                            <m>m1</m>
                          </i>
                          <j Include='@(i)'/>
                        </ItemGroup>
                    </Project>
                ";

            using ProjectFromString projectFromString = new(content);
            Project project = projectFromString.Project;

            project.GetItems("i").First().SetMetadataValue("m", "m2");

            ProjectItem item1 = project.GetItems("i").First();
            ProjectItem item2 = project.GetItems("j").First();

            Assert.AreEqual("m2", item1.GetMetadataValue("m"));
            Assert.AreEqual("m1", item2.GetMetadataValue("m"));

            // Should still point at the same XML items
            Assert.IsTrue(Object.ReferenceEquals(item1.GetMetadata("m").Xml, item2.GetMetadata("m").Xml));
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
        [MSBuildTestMethod]
        public void CopyFromWithItemListExpressionDoesNotCloneDefinitionMetadata()
        {
            string content = @"
                    <Project>
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

            using ProjectFromString projectFromString = new(content);
            Project project = projectFromString.Project;

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
        [MSBuildTestMethod]
        public void CopyFromWithItemListExpressionClonesDefinitionMetadata_Variation()
        {
            string content = @"
                    <Project>
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

            using ProjectFromString projectFromString = new(content);
            Project project = projectFromString.Project;

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
        [MSBuildTestMethod]
        public void CopyWithItemDefinition()
        {
            string content = @"
                    <Project>
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

            using ProjectFromString projectFromString = new(content);
            Project project = projectFromString.Project;

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
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.IsTrue(!Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
        }

        /// <summary>
        /// Repeated copying of items with item definitions should cause the following order of precedence:
        /// 1) direct metadata on the item
        /// 2) item definition metadata on the very first item in the chain
        /// 3) item definition on the next item, and so on until
        /// 4) item definition metadata on the destination item itself
        /// </summary>
        [MSBuildTestMethod]
        public void CopyWithItemDefinition2()
        {
            string content = @"
                    <Project>
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

            using ProjectFromString projectFromString = new(content);
            Project project = projectFromString.Project;

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
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.IsTrue(Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.IsTrue(!Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
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

            ProjectItem item = GetOneItem(content);

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

            ProjectItem item = GetOneItem(content);

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

            ProjectItem item = GetOneItem(content);

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

            ProjectItem item = GetOneItem(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

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

            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            Assert.AreEqual("0", items[0].GetMetadataValue("m0"));
            Assert.AreEqual("1", items[0].GetMetadataValue("m1"));
            Assert.AreEqual(String.Empty, items[0].GetMetadataValue("m2"));
        }

        /// <summary>
        /// Remove a metadatum
        /// </summary>
        [MSBuildTestMethod]
        public void RemoveMetadata()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            item.SetMetadataValue("m", "m1");
            project.ReevaluateIfNecessary();

            bool found = item.RemoveMetadata("m");

            Assert.IsTrue(found);
            Assert.IsTrue(project.IsDirty);
            Assert.AreEqual(String.Empty, item.GetMetadataValue("m"));
            Assert.AreEqual(0, Helpers.Count(item.Xml.Metadata));
        }

        /// <summary>
        /// Attempt to remove a metadatum originating from an item definition.
        /// Should fail if it was not overridden.
        /// </summary>
        [MSBuildTestMethod]
        public void RemoveItemDefinitionMetadataMasked()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinition("i").AddMetadata("m", "m1");
            xml.AddItem("i", "i1").AddMetadata("m", "m2");
            Project project = new Project(xml);
            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

            bool found = item.RemoveMetadata("m");
            Assert.IsTrue(found);
            Assert.AreEqual(0, item.DirectMetadataCount);
            Assert.AreEqual(0, Helpers.Count(item.DirectMetadata));
            Assert.AreEqual("m1", item.GetMetadataValue("m")); // Now originating from definition!
            Assert.IsTrue(project.IsDirty);
            Assert.AreEqual(0, item.Xml.Count);
        }

        /// <summary>
        /// Attempt to remove a metadatum originating from an item definition.
        /// Should fail if it was not overridden.
        /// </summary>
        [MSBuildTestMethod]
        public void RemoveItemDefinitionMetadataNotMasked()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddItemDefinition("i").AddMetadata("m", "m1");
                xml.AddItem("i", "i1");
                Project project = new Project(xml);
                ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

                item.RemoveMetadata("m"); // Should throw
            });
        }
        /// <summary>
        /// Remove a nonexistent metadatum
        /// </summary>
        [MSBuildTestMethod]
        public void RemoveNonexistentMetadata()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            bool found = item.RemoveMetadata("m");

            Assert.IsFalse(found);
            Assert.IsFalse(project.IsDirty);
        }

        /// <summary>
        /// Tests removing built-in metadata.
        /// </summary>
        [MSBuildTestMethod]
        public void RemoveBuiltInMetadata()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddItem("i", "i1");
                Project project = new Project(xml);
                ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

                // This should throw
                item.RemoveMetadata("FullPath");
            });
        }
        /// <summary>
        /// Simple rename
        /// </summary>
        [MSBuildTestMethod]
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
            Assert.IsTrue(project.IsDirty);
            Assert.AreEqual("i2", item.GetMetadataValue("FileName"));
        }

        /// <summary>
        /// Verifies that renaming a ProjectItem whose xml backing is a wildcard doesn't corrupt
        /// the MSBuild evaluation data.
        /// </summary>
        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
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
                FileUtilities.DeleteWithoutTrailingBackslash(projectDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Change item type
        /// </summary>
        [MSBuildTestMethod]
        public void ChangeItemType()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.ItemType = "j";

            Assert.AreEqual("j", item.ItemType);
            Assert.IsTrue(project.IsDirty);
        }

        /// <summary>
        /// Change item type to invalid value
        /// </summary>
        [MSBuildTestMethod]
        public void ChangeItemTypeInvalid()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                Project project = new Project();
                ProjectItem item = project.AddItem("i", "i1")[0];
                project.ReevaluateIfNecessary();

                item.ItemType = "|";
            });
        }
        /// <summary>
        /// Attempt to rename imported item should fail
        /// </summary>
        [MSBuildTestMethod]
        public void RenameImported()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = FileUtilities.GetTemporaryFileName();
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
            });
        }
        /// <summary>
        /// Attempt to set metadata on imported item should fail
        /// </summary>
        [MSBuildTestMethod]
        public void SetMetadataImported()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = FileUtilities.GetTemporaryFileName();
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
            });
        }
        /// <summary>
        /// Attempt to remove metadata on imported item should fail
        /// </summary>
        [MSBuildTestMethod]
        public void RemoveMetadataImported()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = FileUtilities.GetTemporaryFileName();
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
            });
        }

        [MSBuildTestMethod]
        public void SetDirectMetadataShouldEvaluateMetadataValue()
        {
            var projectContents =
@"<Project>
  <PropertyGroup>
    <P>p</P>
  </PropertyGroup>
  <ItemGroup>
    <Foo Include=`f1;f2`/>
    <I Include=`i`/>
  </ItemGroup>
</Project>".Cleanup();

            using (var env = TestEnvironment.Create())
            {
                var project = ObjectModelHelpers.CreateInMemoryProject(env.CreateProjectCollection().Collection, projectContents);

                var metadata = project.GetItems("I").FirstOrDefault().SetMetadataValue("M", "$(P);@(Foo)", true);

                Assert.AreEqual("p;f1;f2", metadata.EvaluatedValue);
                Assert.AreEqual("$(P);@(Foo)", metadata.Xml.Value);
            }
        }

        [MSBuildTestMethod]
        public void SetDirectMetadataWhenSameMetadataComesFromDefinitionGroupShouldAddDirectMetadata()
        {
            var projectContents =
@"<Project>
  <ItemDefinitionGroup>
    <I>
      <M>V</M>
    </I>
  </ItemDefinitionGroup>
  <ItemGroup>
    <I Include=`i`/>
  </ItemGroup>
</Project>".Cleanup();

            using (var env = TestEnvironment.Create())
            {
                var project = ObjectModelHelpers.CreateInMemoryProject(env.CreateProjectCollection().Collection, projectContents);

                var item = project.GetItems("I").FirstOrDefault();
                var metadata = item.SetMetadataValue("M", "V", true);

                Assert.AreEqual("M", metadata.Name);
                Assert.AreEqual("V", metadata.EvaluatedValue);

                Assert.ContainsSingle(item.Xml.Metadata);

                ProjectMetadataElement metadataElement = item.Xml.Metadata.FirstOrDefault();
                Assert.AreEqual("M", metadataElement.Name);
                Assert.AreEqual("V", metadataElement.Value);
            }
        }

        [MSBuildTestMethod]
        public void SetDirectMetadataShouldAffectAllSiblingItems()
        {
            var projectContents =
@"<Project>
  <ItemGroup>
    <Foo Include=`f1;f2`/>
    <I Include=`*.cs;@(Foo);i1`>
      <M1>V1</M1>
    </I>
  </ItemGroup>
</Project>".Cleanup();

            using (var env = TestEnvironment.Create())
            {
                var testProject = env.CreateTestProjectWithFiles(projectContents.Cleanup(), new[] { "a.cs" });

                var project = new Project(testProject.ProjectFile, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, env.CreateProjectCollection().Collection);

                var items = project.GetItems("I");

                Assert.AreEqual(4, items.Count);

                Assert.IsFalse(project.IsDirty);

                items.First().SetMetadataValue("M2", "V2", true);

                Assert.IsTrue(project.IsDirty);

                Assert.AreEqual(2, project.Xml.AllChildren.OfType<ProjectItemElement>().Count());

                foreach (var item in items)
                {
                    var metadata = item.Metadata;

                    Assert.AreEqual(2, metadata.Count);

                    var m1 = metadata.ElementAt(0);
                    Assert.AreEqual("M1", m1.Name);
                    Assert.AreEqual("V1", m1.EvaluatedValue);

                    var m2 = metadata.ElementAt(1);
                    Assert.AreEqual("M2", m2.Name);
                    Assert.AreEqual("V2", m2.EvaluatedValue);
                }

                var metadataElements = items.First().Xml.Metadata;

                Assert.AreEqual(2, metadataElements.Count);

                var me1 = metadataElements.ElementAt(0);
                Assert.AreEqual("M1", me1.Name);
                Assert.AreEqual("V1", me1.Value);

                var me2 = metadataElements.ElementAt(1);
                Assert.AreEqual("M2", me2.Name);
                Assert.AreEqual("V2", me2.Value);
            }
        }

        [MSBuildTestMethod]
        public void SetDirectMetadataShouldUpdateAlreadyExistingDirectMetadata()
        {
            var projectContents =
@"<Project>
  <ItemGroup>
    <Foo Include=`f1;f2`/>
    <I Include=`*.cs;@(Foo);i1`>
      <M1>V1</M1>
    </I>
  </ItemGroup>
</Project>".Cleanup();

            using (var env = TestEnvironment.Create())
            {
                var testProject = env.CreateTestProjectWithFiles(projectContents.Cleanup(), new[] { "a.cs" });

                var project = new Project(testProject.ProjectFile, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, env.CreateProjectCollection().Collection);

                var items = project.GetItems("I");

                Assert.AreEqual(4, items.Count);

                Assert.IsFalse(project.IsDirty);

                items.First().SetMetadataValue("M1", "V2", true);

                Assert.IsTrue(project.IsDirty);

                Assert.AreEqual(2, project.Xml.AllChildren.OfType<ProjectItemElement>().Count());

                foreach (var item in items)
                {
                    var metadata = item.Metadata;

                    Assert.ContainsSingle(metadata);

                    var m1 = metadata.ElementAt(0);
                    Assert.AreEqual("M1", m1.Name);
                    Assert.AreEqual("V2", m1.EvaluatedValue);
                }

                var metadataElements = items.First().Xml.Metadata;

                Assert.ContainsSingle(metadataElements);

                var me1 = metadataElements.ElementAt(0);
                Assert.AreEqual("M1", me1.Name);
                Assert.AreEqual("V2", me1.Value);
            }
        }

        // TODO: Should remove tests go in project item tests, project item instance tests, or both?
        [MSBuildTestMethod]
        public void Remove()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                "<i Include='a;b' />" +
                "<i Remove='b;c' />");

            Assert.ContainsSingle(items);
            Assert.AreEqual("a", items[0].EvaluatedInclude);
        }

        [MSBuildTestMethod]
        public void RemoveAllMatchingItems()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                "<i Include='a;b' />" +
                "<i Include='a;b' />" +
                "<i Remove='b;c' />");

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(@"a;a", string.Join(";", items.Select(i => i.EvaluatedInclude)));
        }

        [MSBuildTestMethod]
        public void RemoveGlob()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                @"<i Include='a.txt;b.cs;bin\foo.cs' />" +
                @"<i Remove='bin\**' />");

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(@"a.txt;b.cs", string.Join(";", items.Select(i => i.EvaluatedInclude)));
        }

        [MSBuildTestMethod]
        public void RemoveItemReference()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                @"<i Include='a;b;c;d' />" +
                @"<j Include='b;d' />" +
                @"<i Remove='@(j)' />");

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual(@"a;c", string.Join(";", items.Select(i => i.EvaluatedInclude)));
        }

        [MSBuildTestMethod]
        [DataRow(@"1.foo;.\2.foo;.\.\3.foo", @"1.foo;.\2.foo;.\.\3.foo")]
        [DataRow(@"1.foo;.\2.foo;.\.\3.foo", @".\1.foo;.\.\2.foo;.\.\.\3.foo")]
        public void RemoveShouldMatchNonCanonicPaths(string include, string remove)
        {
            var content = @"
                            <i Include='" + include + @"'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i>
                            <i Remove='" + remove + @"'/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            Assert.IsEmpty(items);
        }

        [MSBuildTestMethod]
        public void RemoveShouldRespectCondition()
        {
            var projectContents = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<i Include='a;b;c' />" +
                @"<i Condition='0 == 1' Remove='b' />" +
                @"<i Condition='1 == 1' Remove='c' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            Assert.AreEqual(@"a;b", string.Join(";", project.Items.Select(i => i.EvaluatedInclude)));
        }

        /// <summary>
        /// See comment for details: https://github.com/dotnet/msbuild/issues/1475#issuecomment-275520394
        /// </summary>
        [MSBuildTestMethod]
        [Ignore("https://github.com/dotnet/msbuild/issues/1616")]
        public void RemoveWithConditionShouldNotApplyOnItemsIgnoringCondition()
        {
            var projectContents = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<i Include='a;b;c;d' />" +
                @"<i Condition='0 == 1' Remove='b' />" +
                @"<i Condition='1 == 1' Remove='c' />" +
                @"<i Remove='d' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            Assert.AreEqual(@"a;b;c", string.Join(";", project.ItemsIgnoringCondition.Select(i => i.EvaluatedInclude)));
        }

        [MSBuildTestMethod]
        public void RemoveWithItemReferenceOnMatchingMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<I1 Include='a1' M1='1' M2='a'/>
                <I1 Include='b1' M1='2' M2='x'/>
                <I1 Include='c1' M1='3' M2='y'/>
                <I1 Include='d1' M1='4' M2='b'/>

                <I2 Include='a2' M1='x' m2='c'/>
                <I2 Include='b2' M1='2' m2='x'/>
                <I2 Include='c2' M1='3' m2='Y'/>
                <I2 Include='d2' M1='y' m2='d'/>

                <I2 Remove='@(I1)' MatchOnMetadata='m1'/>");

            var project = ObjectModelHelpers.CreateInMemoryProject(content);
            var items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "d2" });

            items.ElementAt(0).GetMetadataValue("M1").ShouldBe("x");
            items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
            items.ElementAt(1).GetMetadataValue("M1").ShouldBe("y");
            items.ElementAt(1).GetMetadataValue("M2").ShouldBe("d");
        }

        [MSBuildTestMethod]
        public void RemoveWithItemReferenceOnCaseInsensitiveMatchingMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<I1 Include='a1' M1='1' M2='a'/>
                <I1 Include='b1' M1='2' M2='x'/>
                <I1 Include='c1' M1='3' M2='y'/>
                <I1 Include='d1' M1='4' M2='b'/>

                <I2 Include='a2' M1='x' m2='c'/>
                <I2 Include='b2' M1='2' m2='x'/>
                <I2 Include='c2' M1='3' m2='Y'/>
                <I2 Include='d2' M1='y' m2='d'/>

                <I2 Remove='@(I1)' MatchOnMetadata='m2' MatchOnMetadataOptions='CaseInsensitive' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(content);
            var items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "d2" });

            items.ElementAt(0).GetMetadataValue("M1").ShouldBe("x");
            items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
            items.ElementAt(1).GetMetadataValue("M1").ShouldBe("y");
            items.ElementAt(1).GetMetadataValue("M2").ShouldBe("d");
        }

        [MSBuildTestMethod]
        public void RemoveWithItemReferenceOnFilePathMatchingMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                $@"<I1 Include='a1' M1='foo.txt\' M2='a'/>
                <I1 Include='b1' M1='foo/bar.cs' M2='x'/>
                <I1 Include='c1' M1='foo/bar.vb' M2='y'/>
                <I1 Include='d1' M1='foo\foo\foo' M2='b'/>
                <I1 Include='e1' M1='a/b/../c/./d' M2='1'/>
                <I1 Include='f1' M1='{Environment.CurrentDirectory}\b\c' M2='6'/>

                <I2 Include='a2' M1='FOO.TXT' m2='c'/>
                <I2 Include='b2' M1='foo/bar.txt' m2='x'/>
                <I2 Include='c2' M1='/foo/BAR.vb\\/' m2='Y'/>
                <I2 Include='d2' M1='foo/foo/foo/' m2='d'/>
                <I2 Include='e2' M1='foo/foo/foo/' m2='c'/>
                <I2 Include='f2' M1='b\c' m2='e'/>
                <I2 Include='g2' M1='b\d\c' m2='f'/>

                <I2 Remove='@(I1)' MatchOnMetadata='m1' MatchOnMetadataOptions='PathLike' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(content);
            var items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));

            if (FileUtilities.IsFileSystemCaseSensitive)
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

        [MSBuildTestMethod]
        public void RemoveWithItemReferenceOnIntrinsicMatchingMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                $@"<I1 Include='foo.txt' />
                <I1 Include='bar.cs' />
                <I1 Include='../bar.cs' />
                <I1 Include='/foo/../bar.txt' />

                <I2 Include='foo.txt' />
                <I2 Include='../foo.txt' />
                <I2 Include='/bar.txt' />
                <I2 Include='/foo/bar.txt' />

                <I2 Remove='@(I1)' MatchOnMetadata='FullPath' MatchOnMetadataOptions='PathLike' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(content);
            var items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));

            items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "../foo.txt", "/foo/bar.txt" });
        }

        [MSBuildTestMethod]
        public void RemoveWithPropertyReferenceInMatchOnMetadata()
        {
            string content =
                @"<Project>
                    <PropertyGroup>
                        <Meta1>v0</Meta1>
                    </PropertyGroup>
                    <ItemGroup>
                        <I1 Include='a1' v0='1' M2='a'/>
                        <I1 Include='b1' v0='2' M2='x'/>
                        <I1 Include='c1' v0='3' M2='y'/>
                        <I1 Include='d1' v0='4' M2='b'/>

                        <I2 Include='a2' v0='x' m2='c'/>
                        <I2 Include='b2' v0='2' m2='x'/>
                        <I2 Include='c2' v0='3' m2='Y'/>
                        <I2 Include='d2' v0='y' m2='d'/>

                        <I2 Remove='@(I1)' MatchOnMetadata='$(Meta1)' />
                    </ItemGroup>
                </Project>";

            using (var env = TestEnvironment.Create())
            {
                var project = ObjectModelHelpers.CreateInMemoryProject(env.CreateProjectCollection().Collection, content);

                var items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));

                items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "d2" });

                items.ElementAt(0).GetMetadataValue("v0").ShouldBe("x");
                items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
                items.ElementAt(1).GetMetadataValue("v0").ShouldBe("y");
                items.ElementAt(1).GetMetadataValue("M2").ShouldBe("d");
            }
        }

        [MSBuildTestMethod]
        public void RemoveWithItemReferenceInMatchOnMetadata()
        {
            string content =
                @"<Project>
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
                </Project>";

            using (var env = TestEnvironment.Create())
            {
                var project = ObjectModelHelpers.CreateInMemoryProject(env.CreateProjectCollection().Collection, content);

                var items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));

                items.Select(i => i.EvaluatedInclude).ShouldBe(new[] { "a2", "c2", "d2" });

                items.ElementAt(0).GetMetadataValue("v0").ShouldBe("x");
                items.ElementAt(0).GetMetadataValue("M2").ShouldBe("c");
                items.ElementAt(1).GetMetadataValue("v0").ShouldBe("3");
                items.ElementAt(1).GetMetadataValue("M2").ShouldBe("Y");
                items.ElementAt(2).GetMetadataValue("v0").ShouldBe("y");
                items.ElementAt(2).GetMetadataValue("M2").ShouldBe("d");
            }
        }

        [MSBuildTestMethod]
        public void KeepWithItemReferenceOnNonmatchingMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<I1 Include='a1' a='1' b='a'/>
                <I1 Include='b1' a='2' b='x'/>
                <I1 Include='c1' a='3' b='y'/>
                <I1 Include='d1' a='4' b='b'/>

                <I2 Include='a2' c='x' d='c'/>
                <I2 Include='b2' c='2' d='x'/>
                <I2 Include='c2' c='3' d='Y'/>
                <I2 Include='d2' c='y' d='d'/>

                <I2 Remove='@(I1)' MatchOnMetadata='e' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(content);
            var items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));

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

        [MSBuildTestMethod]
        public void RemoveMatchingMultipleMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<I1 Include='a1' M1='1' M2='a'/>
                <I1 Include='b1' M1='2' M2='x'/>
                <I1 Include='c1' M1='3' M2='y'/>
                <I1 Include='d1' M1='4' M2='b'/>

                <I2 Include='a2' M1='x' m2='c'/>
                <I2 Include='b2' M1='2' m2='x'/>
                <I2 Include='c2' M1='3' m2='Y'/>
                <I2 Include='d2' M1='y' m2='d'/>

                <I2 Remove='@(I1)' MatchOnMetadata='M1;M2'/>");

            Project project = ObjectModelHelpers.CreateInMemoryProject(content);
            IEnumerable<ProjectItem> items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I2"));
            items.Count().ShouldBe(3);
            items.ElementAt(0).EvaluatedInclude.ShouldBe("a2");
            items.ElementAt(1).EvaluatedInclude.ShouldBe("c2");
            items.ElementAt(2).EvaluatedInclude.ShouldBe("d2");
        }

        [MSBuildTestMethod]
        public void RemoveMultipleItemReferenceOnMatchingMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<I1 Include='a1' M1='1' M2='a'/>
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

                <I3 Remove='@(I1);@(I2)' MatchOnMetadata='M1' />");

            Project project = ObjectModelHelpers.CreateInMemoryProject(content);
            IEnumerable<ProjectItem> items = project.ItemsIgnoringCondition.Where(i => i.ItemType.Equals("I3"));
            items.ShouldBeEmpty();
        }

        [MSBuildTestMethod]
        public void FailWithMetadataItemReferenceOnMatchingMetadata()
        {
            string content = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<I1 Include='a1' M1='1' M2='a'/>
                <I1 Include='b1' M1='2' M2='x'/>
                <I1 Include='c1' M1='3' M2='y'/>
                <I1 Include='d1' M1='4' M2='b'/>

                <I2 Include='a2' M1='x' m2='c'/>
                <I2 Include='b2' M1='2' m2='x'/>
                <I2 Include='c2' M1='3' m2='Y'/>
                <I2 Include='d2' M1='y' m2='d'/>

                <I2 Remove='%(I1.M1)' MatchOnMetadata='M1' />");
            Should.Throw<InvalidProjectFileException>(() => ObjectModelHelpers.CreateInMemoryProject(content))
                .HelpKeyword.ShouldBe("MSBuild.OM_MatchOnMetadataIsRestrictedToReferencedItems");
        }

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        public void UpdateShouldRespectCondition()
        {
            string projectContents = @"<i Include='a;b;c'>
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

            var project = ObjectModelHelpers.CreateInMemoryProject(ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(projectContents));

            var expectedInitial = new Dictionary<string, string>
            {
                {"m1", "m1_contents"}
            };

            var expectedUpdateFromTrue = new Dictionary<string, string>
            {
                {"m1", "from_true"}
            };

            var items = project.Items.ToList();

            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdateFromTrue, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedInitial, items[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedInitial, items[2]);
        }

        /// <summary>
        /// See comment for details: https://github.com/dotnet/msbuild/issues/1475#issuecomment-275520394
        /// Conditions on metadata on appear to be respected even for items ignoring condition (don't know why, but that's what the code does).
        /// </summary>
        [MSBuildTestMethod]
        [Ignore("https://github.com/dotnet/msbuild/issues/1616")]
        public void UpdateWithConditionShouldNotApplyOnItemsIgnoringCondition()
        {
            string projectContents = @"<i Include='a;b;c;d'>
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
                              </i>
                              <i Update='d'>
                                  <m1>from_uncoditioned_update</m1>
                              </i>";

            var project = ObjectModelHelpers.CreateInMemoryProject(ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(projectContents));

            var expectedInitial = new Dictionary<string, string>
            {
                {"m1", "m1_contents"}
            };

            var expectedUpdateFromUnconditionedElement = new Dictionary<string, string>
            {
                {"m1", "from_uncoditioned_update"}
            };

            var itemsIgnoringCondition = project.ItemsIgnoringCondition.ToList();

            ObjectModelHelpers.AssertItemHasMetadata(expectedInitial, itemsIgnoringCondition[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedInitial, itemsIgnoringCondition[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedInitial, itemsIgnoringCondition[2]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdateFromUnconditionedElement, itemsIgnoringCondition[3]);
        }

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        [DataRow("abc", "def", "abc")]
        [DataRow("abc", "de*", "abc")]
        [DataRow("a*c", "def", "abc")]
        [DataRow("abc", "def", "*bc")]
        [DataRow("abc", "d*f", "*bc")]
        [DataRow("*c", "d*f", "*bc")]
        [DataRow("a*", "d*", "abc")]
        public void UpdatesProceedInOrder(string first, string second, string third)
        {
            string contents = $@"
<i Include='abc'>
    <m1>m1_contents</m1>
</i>
<j Include='def'>
    <m1>m1_contents</m1>
</j>
<i Update='{first}'>
    <m1>first</m1>
</i>
<j Update='{second}'>
    <m1>second</m1>
</j>
<i Update='{third}'>
    <m1>third</m1>
</i>
";
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(contents, allItems: true);
            Dictionary<string, string> expectedUpdatei = new Dictionary<string, string>
            {
                {"m1", "third" }
            };
            Dictionary<string, string> expectedUpdatej = new Dictionary<string, string>
            {
                {"m1", "second" }
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdatei, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdatej, items[1]);
        }

        [MSBuildTestMethod]
        public void UpdatingIndividualItemsProceedsInOrder()
        {
            string contents = @"
<i Include='a;b;c'>
    <m1>m1_contents</m1>
</i>
<i Update='a'>
    <m1>second</m1>
</i>
<i Update='b'>
    <m1>third</m1>
</i>
<i Update='c'>
    <m1>fourth</m1>
</i>
<afterFirst Include='@(i)' />
<i Update='*'>
    <m1>sixth</m1>
</i>
<afterSecond Include='@(i)' />
<i Update='b'>
    <m1>seventh</m1>
</i>
<afterThird Include='@(i)' />
<i Update='c'>
    <m1>eighth</m1>
</i>
<afterFourth Include='@(i)' />
";
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(contents, allItems: true);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "second", items[3]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "third", items[4]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "fourth", items[5]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "sixth", items[6]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "sixth", items[7]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "sixth", items[8]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "sixth", items[9]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "seventh", items[10]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "sixth", items[11]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "sixth", items[12]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "seventh", items[13]);
            ObjectModelHelpers.AssertItemHasMetadata("m1", "eighth", items[14]);
        }

        [MSBuildTestMethod]
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

            Assert.AreEqual(2, items.Count);

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[1]);
        }

        [MSBuildTestMethod]
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

            Assert.AreEqual(2, items.Count);

            var expectedMetadata = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"},
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata, items[1]);
        }

        [MSBuildTestMethod]
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

            var exception = Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ObjectModelHelpers.GetItemsFromFragment(content);
            });

            Assert.AreEqual("The required attribute \"Update\" is empty or missing from the element <i>.", exception.Message);
        }

        // Complex metadata: metadata references from the same item; item transforms; correct binding of metadata with same name but different item qualifiers
        [MSBuildTestMethod]
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
                                  <m3 Condition='%(Identity) == a'>value</m3>
                                  <m4 Condition='%(m1) == b'>%(m1)</m4>
                              </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            Assert.AreEqual(3, items.Count);

            var expectedMetadataX = new Dictionary<string, string>
            {
                {"m1", "x"},
            };

            var expectedMetadataA = new Dictionary<string, string>
            {
                {"m1", "a"},
                {"m2", "ax"},
                {"m3", "value"},
            };

            var expectedMetadataB = new Dictionary<string, string>
            {
                {"m1", "b"},
                {"m2", "bx"},
                {"m4", "b"},
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataX, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataB, items[2]);
        }

        [MSBuildTestMethod]
        public void UpdateShouldImportMetadataFromReferencedItem()
        {
            string content = @"
                              <from Include='a;b'>
                                  <m1>%(Identity)-m1</m1>
                                  <m2>%(Identity)-m2</m2>
                              </from>

                              <to Include='a;b;c'>
                                  <m1>m1_contents</m1>
                                  <m2>m2_contents</m2>
                              </to>

                              <to Update='@(from)'>
                                  <m2>%(m2);%(to.m2);%(from.m2)</m2>
                                  <m3 Condition=`'%(Identity);%(to.Identity);%(from.m1)' == 'a;a;a-m1'`>%(from.m1)</m3>
                                  <m4 Condition=`'%(from.m1)' == 'b-m1'`>%(m1)</m4>
                              </to>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            var expectedMetadataA = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents;m2_contents;a-m2"},
                {"m3", "a-m1"},
            };

            var expectedMetadataB = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents;m2_contents;b-m2"},
                {"m4", "m1_contents"},
            };

            var expectedMetadataC = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"}
            };

            items[2].ItemType.ShouldBe("to");

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[2]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataB, items[3]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataC, items[4]);
        }

        [MSBuildTestMethod]
        public void OptimizedRemoveOperationRespectsCondition()
        {
            string content = @"<TheItem Include=""InitialValue"" />
                               <TheItem Remove=""@(TheItem)"" Condition=""false"" />
                               <TheItem Include=""ReplacedValue"" Condition=""false"" /> ";
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            items[0].EvaluatedInclude.ShouldBe("InitialValue");
        }

        [MSBuildTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void EscapeHatchTurnsOffQualifiedMetadataExpansionInUpdateOperation(bool doNotExpandQualifiedMetadataInUpdateOperation)
        {
            using var env = TestEnvironment.Create();

            if (doNotExpandQualifiedMetadataInUpdateOperation)
            {
                env.SetEnvironmentVariable("MSBuildDoNotExpandQualifiedMetadataInUpdateOperation", "non empty value");
            }

            string content = @"
                              <from Include='a'>
                                  <m1>updated contents</m1>
                              </from>

                              <to Include='a'>
                                  <m1>original contents</m1>
                              </to>

                              <to Update='@(from)'>
                                  <m1>%(from.m1)</m1>
                              </to>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            var expectedMetadataA = doNotExpandQualifiedMetadataInUpdateOperation
                ? new Dictionary<string, string>
                {
                    {"m1", string.Empty}
                }
                : new Dictionary<string, string>
                {
                    {"m1", "updated contents"}
                };

            items[1].ItemType.ShouldBe("to");

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[1]);
        }

        [MSBuildTestMethod]
        public void UpdateFromReferencedItemShouldBeCaseInsensitive()
        {
            string content = @"
                              <from Include='A'>
                                  <metadata>m1_contents</metadata>
                              </from>

                              <to Include='a' />

                              <to Update='@(FrOm)' m='%(fRoM.MetaDATA)' />";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            var expectedMetadataA = new Dictionary<string, string>
            {
                {"m", "m1_contents"},
            };

            items[1].ItemType.ShouldBe("to");
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[1]);
        }

        [MSBuildTestMethod]
        public void UpdateMetadataWithoutItemReferenceShouldBeCaseInsensitive()
        {
            string content = @"
                              <to Include='a' />

                              <to Update='A' m='m1_contents' />";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            var expectedMetadataA = new Dictionary<string, string>
            {
                {"m", "m1_contents"},
            };

            items[0].ItemType.ShouldBe("to");
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[0]);
        }

        [MSBuildTestMethod]
        public void UndeclaredQualifiedMetadataReferencesInUpdateShouldResolveToEmptyStrings()
        {
            string content = @"
                              <from1 Include='a'>
                                  <metadata>m1_contents</metadata>
                              </from1>

                              <from2 Include='a'>
                                  <metadata2>m1_contents</metadata2>
                              </from2>

                              <to Include='a' />

                              <to Update='@(from1)' m1='%(nonexistent.metadata)' m2='%(from2.metadata2)'/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            var expectedMetadataA = new Dictionary<string, string>
            {
                { "m1", string.Empty },
                { "m2", string.Empty },
            };

            items[2].ItemType.ShouldBe("to");
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[2]);
        }

        [MSBuildTestMethod]
        public void UpdateShouldImportMetadataFromMultipleReferencedItems()
        {
            string content = @"
                              <from1 Include='x.cs;y.cs'>
                                  <m1>%(Identity)-m1</m1>
                              </from1>

                              <from2 Include='1;2'>
                                  <m2>%(Identity)-m2</m2>
                              </from2>

                              <from3 Include='1;2'>
                                  <m3>%(Identity)-m3</m3>
                              </from3>

                              <to Include='x.cs;2;ccc;1;d;y.cs'>
                                  <m3>m3_contents</m3>
                              </to>

                              <to Update='@(from1);d;c*c;*.cs;@(from2);@(from3)'>
                                  <m2>%(from2.m2);%(from3.m3)</m2>
                                  <m3>%(from1.m1);%(from2.m2)</m3>
                                  <m4>%(from1.m2);%(from2.m1)</m4>
                                  <m5>%(Identity)</m5>
                                  <m6 Condition='%(Identity) == 1'>%(from2.m2)</m6>
                              </to>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            var expectedMetadataX = new Dictionary<string, string>
            {
                {"m2", ";"},
                {"m3", "x.cs-m1;"},
                {"m4", ";"},
                {"m5", "x.cs"},
            };

            var expectedMetadata2 = new Dictionary<string, string>
            {
                {"m2", "2-m2;2-m3"},
                {"m3", ";2-m2"},
                {"m4", ";"},
                {"m5", "2"},
            };

            var expectedMetadataCCC = new Dictionary<string, string>
            {
                {"m2", ";"},
                {"m3", ";"},
                {"m4", ";"},
                {"m5", "ccc"},
            };

            var expectedMetadata1 = new Dictionary<string, string>
            {
                {"m2", "1-m2;1-m3"},
                {"m3", ";1-m2"},
                {"m4", ";"},
                {"m5", "1"},
                {"m6", "1-m2"},
            };

            var expectedMetadataD = new Dictionary<string, string>
            {
                {"m2", ";"},
                {"m3", ";"},
                {"m4", ";"},
                {"m5", "d"},
            };

            var expectedMetadataY = new Dictionary<string, string>
            {
                {"m2", ";"},
                {"m3", "y.cs-m1;"},
                {"m4", ";"},
                {"m5", "y.cs"},
            };

            items[5].ItemType.ShouldBe("from3");
            items[6].ItemType.ShouldBe("to");

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataX, items[6]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata2, items[7]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataCCC, items[8]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadata1, items[9]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataD, items[10]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataY, items[11]);
        }

        [MSBuildTestMethod]
        public void UpdateFromReferencedItemsWithDuplicatesShouldUseLastItemFromEachItemType()
        {
            using var env = TestEnvironment.Create();

            env.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

            string content = @"
                              <to Include='a;b;c'/>

                              <from1 Include='b;a;b'>
                                  <!-- %(Identity) forces re-evaluating each metadata for each item, leading to different DateTime ticks -->
                                  <m>from1:%(Identity):$([System.DateTime]::Now.Ticks)</m>
                              </from1>

                              <from2 Include='a;c;a'>
                                  <!-- %(Identity) forces re-evaluating each metadata for each item, leading to different DateTime ticks -->
                                  <m>from2:%(Identity):$([System.DateTime]::Now.Ticks)</m>
                              </from2>

                              <to Update='@(from1);@(from2)'>
                                  <m1 Condition='%(from1.Identity) == b'>%(from1.m);%(from2.m)</m1>
                                  <m2 Condition='%(Identity) == a'>%(from1.m);%(from2.m)</m2>
                              </to>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            var lastItemMetadataForBFrom1 = LastItemMetadata("from1", "b");
            var lastItemMetadataForAFrom1 = LastItemMetadata("from1", "a");
            var lastItemMetadataForAFrom2 = LastItemMetadata("from2", "a");

            var expectedMetadataB = new Dictionary<string, string>
            {
                {"m1", $"{lastItemMetadataForBFrom1};"},
            };

            var expectedMetadataA = new Dictionary<string, string>()
            {
                {"m2", $"{lastItemMetadataForAFrom1};{lastItemMetadataForAFrom2}"},
            };

            var expectedMetadataC = new Dictionary<string, string>();

            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataA, items[0]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataB, items[1]);
            ObjectModelHelpers.AssertItemHasMetadata(expectedMetadataC, items[2]);

            string LastItemMetadata(string itemType, string itemValue)
            {
                var lastItemMetadata = items.Last(i => i.ItemType.Equals(itemType) && i.EvaluatedInclude.Equals(itemValue)).GetMetadataValue("m");

                lastItemMetadata.ShouldNotBeNullOrEmpty();

                return lastItemMetadata;
            }
        }

        [MSBuildTestMethod]
        public void UpdateFromReferenceItemAndNoMetadataNOOPS()
        {
            string content = @"
                              <to Include='a'/>

                              <from Include='a'>
                                  <m>m_contents</m>
                              </from>

                              <to Update='@(from)' />";

            var items = ObjectModelHelpers.GetItemsFromFragment(content, true).Where(i => i.ItemType.Equals("to")).ToArray();

            items.ShouldNotBeEmpty();

            foreach (var item in items)
            {
                ObjectModelHelpers.AssertItemHasMetadata(null, item);
            }
        }

        [MSBuildTestMethod]
        public void UpdateShouldBeAbleToContainGlobs()
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

            var items = GetItemsFromFragmentWithGlobs(content, "a.foo", "b.foo", "bar1.foo", "bar2.foo");

            Assert.AreEqual(4, items.Count);

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

        [MSBuildTestMethod]
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

            Assert.AreEqual(4, items.Count);

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

        [MSBuildTestMethod]
        public void UpdateShouldBeAbleToContainProperties()
        {
            var content = @"
                    <Project>
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

            Assert.AreEqual(3, items.Count);

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

        [MSBuildTestMethod]
        public void UpdateAndRemoveShouldUseCaseInsensitiveMatching()
        {
            var content = @"
                            <i Include='x;y'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i>
                            <i Update='X'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i>
                            <i Remove='Y'/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            items.ShouldHaveSingleItem();

            var expectedUpdated = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
            };

            ObjectModelHelpers.AssertItemHasMetadata(expectedUpdated, items[0]);
        }

        public static IEnumerable<Object[]> UpdateAndRemoveShouldWorkWithEscapedCharactersTestData
        {
            get
            {
                var expectedMetadata = new[]
                {
                    new Dictionary<string, string> {{"m", "contents"}},
                    new Dictionary<string, string> {{"m", "updated"}}
                };

                // escaped value matches and nonescaped value include
                yield return new object[]
                {
                    ItemWithIncludeUpdateAndRemove,
                    "i;u;r",
                    "%75",
                    "%72",
                    new[] {"i", "u"},
                    expectedMetadata
                };

                // escaped value matches and escaped value include
                yield return new object[]
                {
                    ItemWithIncludeUpdateAndRemove,
                    "i;%75;%72",
                    "%75",
                    "%72",
                    new[] {"i", "u"},
                    expectedMetadata
                };

                // unescaped value matches and escaped value include
                yield return new object[]
                {
                    ItemWithIncludeUpdateAndRemove,
                    "i;%75;%72",
                    "u",
                    "r",
                    new[] {"i", "u"},
                    expectedMetadata
                };

                // escaped glob matches and nonescaped value include
                yield return new object[]
                {
                    ItemWithIncludeUpdateAndRemove,
                    "i;u;r",
                    "*%75*",
                    "*%72*",
                    new[] {"i", "u"},
                    expectedMetadata
                };

                // escaped glob matches and escaped value include
                yield return new object[]
                {
                    ItemWithIncludeUpdateAndRemove,
                    "i;%75;%72",
                    "*%75*",
                    "*%72*",
                    new[] {"i", "u"},
                    expectedMetadata
                };

                // escaped matching items as globs containing escaped wildcards; treated as normal values
                yield return new object[]
                {
                    ItemWithIncludeUpdateAndRemove,
                    "i;u;r;%2A%75%2A;%2A%72%2A",
                    "%2A%75%2A",
                    "%2A%72%2A",
                    new[] {"i", "u", "r", "*u*"},
                    new[]
                    {
                        new Dictionary<string, string> {{"m", "contents"}},
                        new Dictionary<string, string> {{"m", "contents"}},
                        new Dictionary<string, string> {{"m", "contents"}},
                        new Dictionary<string, string> {{"m", "updated"}}
                    }
                };
            }
        }

        [MSBuildTestMethod]
        [DynamicData(nameof(UpdateAndRemoveShouldWorkWithEscapedCharactersTestData))]
        public void UpdateAndRemoveShouldWorkWithEscapedCharacters(string projectContents, string include, string update, string remove, string[] expectedInclude, Dictionary<string, string>[] expectedMetadata)
        {
            var formattedProjectContents = string.Format(projectContents, include, update, remove);
            ObjectModelHelpers.AssertItemEvaluationFromProject(formattedProjectContents, Array.Empty<string>(), expectedInclude, expectedMetadata);
        }

        [MSBuildTestMethod]
        public void UpdateAndRemoveShouldNotUseGlobMatchingOnEscapedGlobsFromReferencedItems()
        {
            var project = @"
                    <Project>
                        <ItemGroup>
                            <!-- %2A is an escaped '*' character -->
                            <from1 Include='%2A.cs' />
                            <from2 Include='%2A.js' />
                            <i Include='1.cs;2.js' />

                            <i Update='@(from1)'>
                               <m>updated</m>
                            </i>

                            <i Remove='@(from2)'/>
                        </ItemGroup>
                    </Project>
                ";

            ObjectModelHelpers.AssertItemEvaluationFromGenericItemEvaluator(
                (p, c) =>
                {
                    return new Project(p, new Dictionary<string, string>(), MSBuildConstants.CurrentToolsVersion, c)
                        .Items
                        .Where(i => i.ItemType.Equals("i"))
                        .Select(i => (ObjectModelHelpers.ITestItem)new ObjectModelHelpers.ProjectItemTestItemAdapter(i))
                        .ToList();
                },
                project,
                inputFiles: Array.Empty<string>(),
                expectedInclude: new[] { "1.cs", "2.js" },
                expectedMetadataPerItem: null);
        }

        [MSBuildTestMethod]
        [DataRow(@"1.foo;.\2.foo;.\.\3.foo", @"1.foo;.\2.foo;.\.\3.foo")]
        [DataRow(@"1.foo;.\2.foo;.\.\3.foo", @".\1.foo;.\.\2.foo;.\.\.\3.foo")]
        public void UpdateShouldMatchNonCanonicPaths(string include, string update)
        {
            var content = @"
                            <i Include='" + include + @"'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i>
                            <i Update='" + update + @"'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            var expectedUpdated = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
            };

            foreach (var item in items)
            {
                ObjectModelHelpers.AssertItemHasMetadata(expectedUpdated, item);
            }
        }

        private static List<ProjectItem> GetItemsFromFragmentWithGlobs(string itemGroupFragment, params string[] globFiles)
        {
            var formattedProjectContents = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(itemGroupFragment);

            List<ProjectItem> itemsFromFragmentWithGlobs;

            using (var env = TestEnvironment.Create())
            {
                var testProject = env.CreateTestProjectWithFiles(formattedProjectContents, globFiles);
                itemsFromFragmentWithGlobs = Helpers.MakeList(new Project(testProject.ProjectFile).GetItems("i"));
            }

            return itemsFromFragmentWithGlobs;
        }

        /// <summary>
        /// Get the item of type "i" using the item Xml fragment provided.
        /// If there is more than one, fail.
        /// </summary>
        private static ProjectItem GetOneItemFromFragment(string fragment)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(fragment);

            Assert.ContainsSingle(items);
            return items[0];
        }

        /// <summary>
        /// Get the item of type "i" in the project provided.
        /// If there is more than one, fail.
        /// </summary>
        private static ProjectItem GetOneItem(string content)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            Assert.ContainsSingle(items);
            return items[0];
        }

        /// <summary>
        /// Item metadata "Filename" should not depends on platform specific slashes.
        /// </summary>
        [MSBuildTestMethod]
        public void FileNameMetadataEvaluationShouldNotDependsFromPlatformSpecificSlashes()
        {
            using (var env = TestEnvironment.Create())
            using (var projectCollection = new ProjectCollection())
            {
                var testFiles = env.CreateTestProjectWithFiles(@"<?xml version=`1.0` encoding=`utf-8`?>
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` DefaultTargets=`Validate` xmlns=`msbuildnamespace`>
                      <ItemGroup>
                        <A Include=`A\B\C\D.cs` />
                        <B Include=`@(A->'%(Filename)_test.ext')` />
                      </ItemGroup>
                    </Project>");
                var project = new Project(testFiles.ProjectFile, new Dictionary<string, string>(), null, projectCollection);
                var buildManager = BuildManager.DefaultBuildManager;
                var projectInstance = buildManager.GetProjectInstanceForBuild(project);
                var itemB = projectInstance.Items.Single(i => i.ItemType == "B").EvaluatedInclude;
                itemB.ShouldBe("D_test.ext");
            }
        }
    }

    [TestClass]
    public class ProjectItemWithOptimizations_Tests : ProjectItem_Tests
    {
        public ProjectItemWithOptimizations_Tests()
        {
            // Make sure we always use the dictionary-based Remove logic.
            _env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", "0");
        }
    }
}
