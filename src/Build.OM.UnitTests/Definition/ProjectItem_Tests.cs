// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Engine.UnitTests.Globbing;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectItem
    /// </summary>
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
        private DummyMappedDrive _mappedDrive = null;

        public ProjectItem_Tests()
        {
            _env = TestEnvironment.Create();
        }

        public void Dispose()
        {
            _env.Dispose();
            _mappedDrive?.Dispose();
        }

        /// <summary>
        /// Project getter
        /// </summary>
        [Fact]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];

            Assert.True(Object.ReferenceEquals(project, item.Project));
        }

        /// <summary>
        /// No metadata, simple case
        /// </summary>
        [Fact]
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

            Assert.NotNull(item.Xml);
            Assert.Equal("i", item.ItemType);
            Assert.Equal("i1", item.EvaluatedInclude);
            Assert.Equal("i1", item.UnevaluatedInclude);
            Assert.False(item.Metadata.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Read off metadata
        /// </summary>
        [Fact]
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

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));
            ProjectMetadata m0 = item.GetMetadata("m0");
            ProjectMetadata m1 = item.GetMetadata("m1");

            ProjectItemDefinition definition = project.ItemDefinitions["i"];
            ProjectMetadata idm0 = definition.GetMetadata("m0");
            ProjectMetadata idm1 = definition.GetMetadata("m1");

            Assert.True(Object.ReferenceEquals(m0, idm0));
            Assert.False(Object.ReferenceEquals(m1, idm1));
        }

        /// <summary>
        /// Get metadata values inherited from item definitions
        /// </summary>
        [Fact]
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

            Assert.Null(item.GetMetadata("m0"));
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
            });
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
            });
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
            });
        }
        /// <summary>
        /// Metadata enumerator should only return custom metadata
        /// </summary>
        [Fact]
        public void MetadataEnumeratorExcludesBuiltInMetadata()
        {
            ProjectItem item = GetOneItemFromFragment(@"<i Include='c:\foo\bar.baz'/>");

            Assert.False(item.Metadata.GetEnumerator().MoveNext());
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
                path = Microsoft.Build.Shared.FileUtilities.GetTemporaryFileName();
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

        [Theory]
        [InlineData(@"<i Condition='false' Include='\**\*.cs'/>")]
        [InlineData(@"<i Condition='false' Include='/**/*.cs'/>")]
        [InlineData(@"<i Condition='false' Include='/**\*.cs'/>")]
        [InlineData(@"<i Condition='false' Include='\**/*.cs'/>")]
        public void FullFileSystemScanGlobWithFalseCondition(string itemDefinition)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(itemDefinition, allItems: false, ignoreCondition: true);
            items.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(@"<i Condition='false' Include='somedir\**\*.cs'/>")]
        [InlineData(@"<i Condition='false' Include='somedir/**/*.cs'/>")]
        [InlineData(@"<i Condition='false' Include='somedir/**\*.cs'/>")]
        [InlineData(@"<i Condition='false' Include='somedir\**/*.cs'/>")]
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
        [Fact]
        public void Exclude()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment("<i Include='a;b' Exclude='b;c'/>");

            Assert.Single(items);
            Assert.Equal("a", items[0].EvaluatedInclude);
        }

        /// <summary>
        /// Exclude against an include with item vectors in it
        /// </summary>
        [Fact]
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
            Assert.Equal(7, items.Count);
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c", "z", "a", "c", "u" }, items);
        }

        [Theory]
        // items as strings: escaped includes appear as unescaped
        [InlineData(ItemWithIncludeAndExclude,
            "%61;%62",
            "b",
            new string[0],
            new[] { "a" })]
        // items as strings: escaped include matches non-escaped exclude
        [InlineData(ItemWithIncludeAndExclude,
            "%61",
            "a",
            new string[0],
            new string[0])]
        // items as strings: non-escaped include matches escaped exclude
        [InlineData(ItemWithIncludeAndExclude,
            "a",
            "%61",
            new string[0],
            new string[0])]
        // items as files: non-escaped wildcard include matches escaped non-wildcard character
        [InlineData(ItemWithIncludeAndExclude,
            "a?b",
            "a%40b",
            new[] { "acb", "a@b" },
            new[] { "acb" })]
        // items as files: non-escaped non-wildcard include matches escaped non-wildcard character
        [InlineData(ItemWithIncludeAndExclude,
           "acb;a@b",
           "a%40b",
           new string[0],
           new[] { "acb" })]
        // items as files: escaped wildcard include matches escaped non-wildcard exclude
        [InlineData(ItemWithIncludeAndExclude,
            "a%40*b",
            "a%40bb",
            new[] { "a@b", "a@ab", "a@bb" },
            new[] { "a@ab", "a@b" })]
        // items as files: escaped wildcard include matches escaped wildcard exclude
        [InlineData(ItemWithIncludeAndExclude,
            "a%40*b",
            "a%40?b",
            new[] { "a@b", "a@ab", "a@bb" },
            new[] { "a@b" })]
        // items as files: non-escaped recursive wildcard include matches escaped recursive wildcard exclude
        [InlineData(ItemWithIncludeAndExclude,
           @"**\a*b",
           @"**\a*%78b",
           new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" },
           new[] { "aab", @"dir\abb" })]
        // items as files: include with non-escaped glob does not match exclude with escaped wildcard character.
        // The exclude is treated as a literal, not a glob, and therefore should not match the input files
        [InlineData(ItemWithIncludeAndExclude,
            @"**\a*b",
            @"**\a%2Axb",
            new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" },
            new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" })]
        public void IncludeExcludeWithEscapedCharacters(string projectContents, string includeString, string excludeString, string[] inputFiles, string[] expectedInclude)
        {
            TestIncludeExcludeWithDifferentSlashes(projectContents, includeString, excludeString, inputFiles, expectedInclude);
        }

        [Theory]
        // items as strings: include with both escaped and unescaped glob should be treated as literal and therefore not match against files as a glob
        [InlineData(ItemWithIncludeAndExclude,
            @"**\a%2Axb",
            @"foo",
            new[] { "aab", "aaxb", @"dir\abb", @"dir\abxb" },
            new[] { @"**\a*xb" })]
        // Include with both escaped and unescaped glob does not match exclude with escaped wildcard character which has a different slash orientation
        // The presence of the escaped and unescaped glob should make things behave as strings-which-are-not-paths and not as strings-which-are-paths
        [InlineData(ItemWithIncludeAndExclude,
            @"**\a%2Axb",
            @"**/a%2Axb",
            new string[0],
            new[] { @"**\a*xb" })]
        // Slashes are not normalized when contents is not a path
        [InlineData(ItemWithIncludeAndExclude,
            @"a/b/foo::||bar;a/b/foo::||bar/;a/b/foo::||bar\;a/b\foo::||bar",
            @"a/b/foo::||bar",
            new string[0],
            new[] { "a/b/foo::||bar/", @"a/b/foo::||bar\", @"a/b\foo::||bar" })]
        public void IncludeExcludeWithNonPathContents(string projectContents, string includeString, string excludeString, string[] inputFiles, string[] expectedInclude)
        {
            TestIncludeExclude(projectContents, inputFiles, expectedInclude, includeString, excludeString, normalizeSlashes: false);
        }

        public static IEnumerable<object[]> IncludesAndExcludesWithWildcardsTestData => GlobbingTestData.IncludesAndExcludesWithWildcardsTestData;

        [Theory]
        [MemberData(nameof(IncludesAndExcludesWithWildcardsTestData))]
        public void ExcludeVectorWithWildCards(string includeString, string excludeString, string[] inputFiles, string[] expectedInclude, bool makeExpectedIncludeAbsolute)
        {
            TestIncludeExcludeWithDifferentSlashes(ItemWithIncludeAndExclude, includeString, excludeString, inputFiles, expectedInclude, makeExpectedIncludeAbsolute);
        }

        [Theory]
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [Theory]
        [InlineData(@"\**\*.log")]
        [InlineData(@"$(empty)\**\*.log")]
        [InlineData(@"\$(empty)**\*.log")]
        [InlineData(@"\*$(empty)*\*.log")]
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
        [InlineData(@"%DRIVE%:\**\*.log")]
        [InlineData(@"%DRIVE%:$(empty)\**\*.log")]
        [InlineData(@"%DRIVE%:\**")]
        [InlineData(@"%DRIVE%:\\**")]
        [InlineData(@"%DRIVE%:\\\\\\\\**")]
        [InlineData(@"%DRIVE%:\**\*.cs")]
        public void ProjectGetterResultsInWindowsDriveEnumerationWarning(string unevaluatedInclude)
        {
            var mappedDrive = GetDummyMappedDrive();
            unevaluatedInclude = UpdatePathToMappedDrive(unevaluatedInclude, mappedDrive.MappedDriveLetter);
            ProjectGetterResultsInDriveEnumerationWarning(unevaluatedInclude);
        }

        [UnixOnlyTheory]
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/8373")]
        [InlineData(@"/**/*.log")]
        [InlineData(@"$(empty)/**/*.log")]
        [InlineData(@"/$(empty)**/*.log")]
        [InlineData(@"/*$(empty)*/*.log")]
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
                    ProjectCollection projectCollection = new ProjectCollection();
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
        [Theory]
        [InlineData(
            ImportProjectElement,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            null)]

        // LazyItem.IncludeOperation
        [InlineData(
            ItemWithIncludeAndExclude,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        // LazyItem.IncludeOperation for Exclude
        [InlineData(
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
        [InlineData(
            ImportProjectElement,
            @"%DRIVE%:\**\*.targets",
            null)]

        // LazyItem.IncludeOperation
        [InlineData(
            ItemWithIncludeAndExclude,
            @"%DRIVE%:$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        // LazyItem.IncludeOperation for Exclude
        [InlineData(
            ItemWithIncludeAndExclude,
            @"$(EmptyProperty)\*.cs",
            @"%DRIVE%:\$(Microsoft_WindowsAzure_EngSys)**")]
        public void LogWindowsWarningUponProjectInstanceCreationFromDriveEnumeratingContent(string content, string placeHolder, string excludePlaceHolder = null)
        {
            var mappedDrive = GetDummyMappedDrive();
            placeHolder = UpdatePathToMappedDrive(placeHolder, mappedDrive.MappedDriveLetter);
            excludePlaceHolder = UpdatePathToMappedDrive(excludePlaceHolder, mappedDrive.MappedDriveLetter);
            content = string.Format(content, placeHolder, excludePlaceHolder);
            CleanContentsAndCreateProjectInstanceFromFileWithDriveEnumeratingWildcard(content, false);
        }

        private DummyMappedDrive GetDummyMappedDrive()
        {
            if (NativeMethods.IsWindows)
            {
                // let's create the mapped drive only once it's needed by any test, then let's reuse;
                _mappedDrive ??= new DummyMappedDrive();
            }

            return _mappedDrive;
        }

        private static string UpdatePathToMappedDrive(string path, char driveLetter)
        {
            const string drivePlaceholder = "%DRIVE%";
            // if this seems to be rooted path - replace with the dummy mount
            if (!string.IsNullOrEmpty(path) && path.StartsWith(drivePlaceholder))
            {
                path = driveLetter + path.Substring(drivePlaceholder.Length);
            }
            return path;
        }

        [UnixOnlyTheory]
        [ActiveIssue("https://github.com/dotnet/msbuild/issues/8373")]
        [InlineData(
            ImportProjectElement,
            @"\**\*.targets",
            null)]

        // LazyItem.IncludeOperation
        [InlineData(
            ItemWithIncludeAndExclude,
            @"$(Microsoft_WindowsAzure_EngSys)\**\*",
            @"$(Microsoft_WindowsAzure_EngSys)\*.pdb;$(Microsoft_WindowsAzure_EngSys)\Microsoft.WindowsAzure.Storage.dll;$(Microsoft_WindowsAzure_EngSys)\Certificates\**\*")]

        // LazyItem.IncludeOperation for Exclude
        [InlineData(
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

        [Theory]
        // exclude matches include; file is next to project file
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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
        [InlineData(ItemWithIncludeAndExclude,
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

        [Theory]
        // exclude globbing cone at project level;
        [InlineData(
            "../a.cs;b.cs", // include string
            "**/*.cs", // exclude string
            new[] { "a.cs", "ProjectDir/b.cs" }, // files to create relative to the test root dir
            "ProjectDir", // relative path from test root to project
            new[] { "../a.cs" }) // expected items
            ]
        // exclude globbing cone below project level;
        [InlineData(
            "a.cs;a/b.cs",
            "a/**/*.cs",
            new[] { "a.cs", "a/b.cs" },
            "",
            new[] { "a.cs" })]
        // exclude globbing above project level;
        [InlineData(
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

        [Theory(Skip = "https://github.com/dotnet/msbuild/issues/1576")]
        [InlineData(
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
        [Fact]
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

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            project.GetItems("i").First().SetMetadataValue("m", "m2");

            ProjectItem item1 = project.GetItems("i").First();
            ProjectItem item2 = project.GetItems("j").First();

            Assert.Equal("m2", item1.GetMetadataValue("m"));
            Assert.Equal("m1", item2.GetMetadataValue("m"));

            // Should still point at the same XML items
            Assert.True(Object.ReferenceEquals(item1.GetMetadata("m").Xml, item2.GetMetadata("m").Xml));
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
            Assert.True(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.True(!Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
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
            Assert.True(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("l").Xml, project.GetItems("m").First().GetMetadata("l").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("m").Xml, project.GetItems("m").First().GetMetadata("m").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("i").First().GetMetadata("n").Xml, project.GetItems("m").First().GetMetadata("n").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("o").Xml, project.GetItems("k").First().GetMetadata("o").Xml));
            Assert.True(Object.ReferenceEquals(project.GetItems("k").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
            Assert.True(!Object.ReferenceEquals(project.GetItems("j").First().GetMetadata("p").Xml, project.GetItems("m").First().GetMetadata("p").Xml));
        }

        /// <summary>
        /// Metadata on items can refer to metadata above
        /// </summary>
        [Fact]
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
                    <Project>
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
                    <Project>
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
                    <Project>
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
        [Fact]
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

            Assert.Equal("h0;i0", items[1].GetMetadataValue("m1"));
        }

        /// <summary>
        /// Metadata on items' conditions can refer to item lists
        /// </summary>
        [Fact]
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

            Assert.True(found);
            Assert.True(project.IsDirty);
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
            Assert.True(found);
            Assert.Equal(0, item.DirectMetadataCount);
            Assert.Equal(0, Helpers.Count(item.DirectMetadata));
            Assert.Equal("m1", item.GetMetadataValue("m")); // Now originating from definition!
            Assert.True(project.IsDirty);
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
            });
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

            Assert.False(found);
            Assert.False(project.IsDirty);
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
            });
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
            Assert.True(project.IsDirty);
            Assert.Equal("i2", item.GetMetadataValue("FileName"));
        }

        /// <summary>
        /// Verifies that renaming a ProjectItem whose xml backing is a wildcard doesn't corrupt
        /// the MSBuild evaluation data.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
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
            Assert.True(project.IsDirty);
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
            });
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
                    file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFileName();
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
        [Fact]
        public void SetMetadataImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFileName();
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
        [Fact]
        public void RemoveMetadataImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFileName();
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

        [Fact]
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

                Assert.Equal("p;f1;f2", metadata.EvaluatedValue);
                Assert.Equal("$(P);@(Foo)", metadata.Xml.Value);
            }
        }

        [Fact]
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

                Assert.Equal("M", metadata.Name);
                Assert.Equal("V", metadata.EvaluatedValue);

                Assert.Single(item.Xml.Metadata);

                ProjectMetadataElement metadataElement = item.Xml.Metadata.FirstOrDefault();
                Assert.Equal("M", metadataElement.Name);
                Assert.Equal("V", metadataElement.Value);
            }
        }

        [Fact]
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

                Assert.Equal(4, items.Count);

                Assert.False(project.IsDirty);

                items.First().SetMetadataValue("M2", "V2", true);

                Assert.True(project.IsDirty);

                Assert.Equal(2, project.Xml.AllChildren.OfType<ProjectItemElement>().Count());

                foreach (var item in items)
                {
                    var metadata = item.Metadata;

                    Assert.Equal(2, metadata.Count);

                    var m1 = metadata.ElementAt(0);
                    Assert.Equal("M1", m1.Name);
                    Assert.Equal("V1", m1.EvaluatedValue);

                    var m2 = metadata.ElementAt(1);
                    Assert.Equal("M2", m2.Name);
                    Assert.Equal("V2", m2.EvaluatedValue);
                }

                var metadataElements = items.First().Xml.Metadata;

                Assert.Equal(2, metadataElements.Count);

                var me1 = metadataElements.ElementAt(0);
                Assert.Equal("M1", me1.Name);
                Assert.Equal("V1", me1.Value);

                var me2 = metadataElements.ElementAt(1);
                Assert.Equal("M2", me2.Name);
                Assert.Equal("V2", me2.Value);
            }
        }

        [Fact]
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

                Assert.Equal(4, items.Count);

                Assert.False(project.IsDirty);

                items.First().SetMetadataValue("M1", "V2", true);

                Assert.True(project.IsDirty);

                Assert.Equal(2, project.Xml.AllChildren.OfType<ProjectItemElement>().Count());

                foreach (var item in items)
                {
                    var metadata = item.Metadata;

                    Assert.Single(metadata);

                    var m1 = metadata.ElementAt(0);
                    Assert.Equal("M1", m1.Name);
                    Assert.Equal("V2", m1.EvaluatedValue);
                }

                var metadataElements = items.First().Xml.Metadata;

                Assert.Single(metadataElements);

                var me1 = metadataElements.ElementAt(0);
                Assert.Equal("M1", me1.Name);
                Assert.Equal("V2", me1.Value);
            }
        }

        // TODO: Should remove tests go in project item tests, project item instance tests, or both?
        [Fact]
        public void Remove()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                "<i Include='a;b' />" +
                "<i Remove='b;c' />");

            Assert.Single(items);
            Assert.Equal("a", items[0].EvaluatedInclude);
        }

        [Fact]
        public void RemoveAllMatchingItems()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                "<i Include='a;b' />" +
                "<i Include='a;b' />" +
                "<i Remove='b;c' />");

            Assert.Equal(2, items.Count);
            Assert.Equal(@"a;a", string.Join(";", items.Select(i => i.EvaluatedInclude)));
        }

        [Fact]
        public void RemoveGlob()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                @"<i Include='a.txt;b.cs;bin\foo.cs' />" +
                @"<i Remove='bin\**' />");

            Assert.Equal(2, items.Count);
            Assert.Equal(@"a.txt;b.cs", string.Join(";", items.Select(i => i.EvaluatedInclude)));
        }

        [Fact]
        public void RemoveItemReference()
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(
                @"<i Include='a;b;c;d' />" +
                @"<j Include='b;d' />" +
                @"<i Remove='@(j)' />");

            Assert.Equal(2, items.Count);
            Assert.Equal(@"a;c", string.Join(";", items.Select(i => i.EvaluatedInclude)));
        }

        [Theory]
        [InlineData(@"1.foo;.\2.foo;.\.\3.foo", @"1.foo;.\2.foo;.\.\3.foo")]
        [InlineData(@"1.foo;.\2.foo;.\.\3.foo", @".\1.foo;.\.\2.foo;.\.\.\3.foo")]
        public void RemoveShouldMatchNonCanonicPaths(string include, string remove)
        {
            var content = @"
                            <i Include='" + include + @"'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i>
                            <i Remove='" + remove + @"'/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            Assert.Empty(items);
        }

        [Fact]
        public void RemoveShouldRespectCondition()
        {
            var projectContents = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<i Include='a;b;c' />" +
                @"<i Condition='0 == 1' Remove='b' />" +
                @"<i Condition='1 == 1' Remove='c' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            Assert.Equal(@"a;b", string.Join(";", project.Items.Select(i => i.EvaluatedInclude)));
        }

        /// <summary>
        /// See comment for details: https://github.com/dotnet/msbuild/issues/1475#issuecomment-275520394
        /// </summary>
        [Fact(Skip = "https://github.com/dotnet/msbuild/issues/1616")]
        public void RemoveWithConditionShouldNotApplyOnItemsIgnoringCondition()
        {
            var projectContents = ObjectModelHelpers.FormatProjectContentsWithItemGroupFragment(
                @"<i Include='a;b;c;d' />" +
                @"<i Condition='0 == 1' Remove='b' />" +
                @"<i Condition='1 == 1' Remove='c' />" +
                @"<i Remove='d' />");

            var project = ObjectModelHelpers.CreateInMemoryProject(projectContents);

            Assert.Equal(@"a;b;c", string.Join(";", project.ItemsIgnoringCondition.Select(i => i.EvaluatedInclude)));
        }

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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
        [Fact(Skip = "https://github.com/dotnet/msbuild/issues/1616")]
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

        [Theory]
        [InlineData("abc", "def", "abc")]
        [InlineData("abc", "de*", "abc")]
        [InlineData("a*c", "def", "abc")]
        [InlineData("abc", "def", "*bc")]
        [InlineData("abc", "d*f", "*bc")]
        [InlineData("*c", "d*f", "*bc")]
        [InlineData("a*", "d*", "abc")]
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

        [Fact]
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
                ObjectModelHelpers.GetItemsFromFragment(content);
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
                                  <m3 Condition='%(Identity) == a'>value</m3>
                                  <m4 Condition='%(m1) == b'>%(m1)</m4>
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

        [Fact]
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

        [Fact]
        public void OptimizedRemoveOperationRespectsCondition()
        {
            string content = @"<TheItem Include=""InitialValue"" />
                               <TheItem Remove=""@(TheItem)"" Condition=""false"" />
                               <TheItem Include=""ReplacedValue"" Condition=""false"" /> ";
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, true);

            items[0].EvaluatedInclude.ShouldBe("InitialValue");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Theory]
        [MemberData(nameof(UpdateAndRemoveShouldWorkWithEscapedCharactersTestData))]
        public void UpdateAndRemoveShouldWorkWithEscapedCharacters(string projectContents, string include, string update, string remove, string[] expectedInclude, Dictionary<string, string>[] expectedMetadata)
        {
            var formattedProjectContents = string.Format(projectContents, include, update, remove);
            ObjectModelHelpers.AssertItemEvaluationFromProject(formattedProjectContents, Array.Empty<string>(), expectedInclude, expectedMetadata);
        }

        [Fact]
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

        [Theory]
        [InlineData(@"1.foo;.\2.foo;.\.\3.foo", @"1.foo;.\2.foo;.\.\3.foo")]
        [InlineData(@"1.foo;.\2.foo;.\.\3.foo", @".\1.foo;.\.\2.foo;.\.\.\3.foo")]
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

            Assert.Single(items);
            return items[0];
        }

        /// <summary>
        /// Get the item of type "i" in the project provided.
        /// If there is more than one, fail.
        /// </summary>
        private static ProjectItem GetOneItem(string content)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItems(content);

            Assert.Single(items);
            return items[0];
        }

        /// <summary>
        /// Item metadata "Filename" should not depends on platform specific slashes.
        /// </summary>
        [Fact]
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

    public class ProjectItemWithOptimizations_Tests : ProjectItem_Tests
    {
        public ProjectItemWithOptimizations_Tests()
        {
            // Make sure we always use the dictionary-based Remove logic.
            _env.SetEnvironmentVariable("MSBUILDDICTIONARYBASEDITEMREMOVETHRESHOLD", "0");
        }
    }
}
