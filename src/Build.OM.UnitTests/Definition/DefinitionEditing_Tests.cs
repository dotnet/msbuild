// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for editing through the definition model
    /// </summary>
    public class DefinitionEditing_Tests
    {
        public delegate void SetupProject(Project p);

        public static IEnumerable<object[]> ItemElementsThatRequireSplitting
        {
            get
            {
                // explode on semicolon separated literals
                yield return new object[]
                {
                    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <ItemGroup>
                          <i Include=""i1;i2"">
                            {0}
                          </i>
                        </ItemGroup>
                      </Project>",
                    0, // operate on first item
                    null // project setup that should be performed after the project is loaded. Used to simulate items coming from globs
                };
                // explode on item coming from property with one item
                yield return new object[]
                {
                    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <PropertyGroup>
                          <p>i1</p>
                        </PropertyGroup>
                        <ItemGroup>
                          <i Include=""$(p)"" >
                            {0}
                          </i>
                        </ItemGroup>
                      </Project>",
                    0,
                    null
                };
                // explode on item coming from property with multiple items
                yield return new object[]
                {
                    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <PropertyGroup>
                          <p>i1;i2</p>
                        </PropertyGroup>
                        <ItemGroup>
                          <i Include=""$(p)"" >
                            {0}
                          </i>
                        </ItemGroup>
                      </Project>",
                    0,
                    null
                };
                // explode on item coming from item reference with one item
                yield return new object[]
                {
                    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <ItemGroup>
                          <a Include=""i1"" />
                          <i Include=""@(a)"" >
                            {0}
                          </i>
                        </ItemGroup>
                      </Project>",
                    1, // operate on the second item. It is produced by the second item element
                    null
                };
                // explode on item coming from item reference with multiple items
                yield return new object[]
                {
                    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <ItemGroup>
                          <a Include=""i1;i2"" />
                          <i Include=""@(a)"" >
                            {0}
                          </i>
                        </ItemGroup>
                      </Project>",
                    2, // operate on the third item. It is produced by the second item element
                    null
                };
            }
        }

        public static IEnumerable<object[]> ItemElementsWithGlobsThatRequireSplitting
        {
            get
            {
                // explode on item coming from glob that expands to one item
                yield return new object[]
                {
                    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <ItemGroup>
                          <a Include=""*.foo"">
                            {0}
                          </a>
                        </ItemGroup>
                      </Project>",
                    0,
                    new SetupProject(p => { p.AddItem("a", "1.foo"); })
                };
                // explode on item coming from glob that expands to multiple items
                yield return new object[]
                {
                    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <ItemGroup>
                          <a Include=""*.foo"">
                            {0}
                          </a>
                        </ItemGroup>
                      </Project>",
                    0,
                    new SetupProject(p =>
                    {
                        p.AddItem("a", "1.foo");
                        p.AddItem("a", "2.foo");
                    })
                };
            }
        }

        /// <summary>
        /// Add an item to an empty project
        /// </summary>
        [Fact]
        public void AddItem()
        {
            Project project = new Project();

            project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            List<ProjectItem> items = Helpers.MakeList(project.Items);
            Assert.Equal(1, items.Count);
            Assert.Equal("i", items[0].ItemType);
            Assert.Equal("i1", items[0].EvaluatedInclude);
            Assert.Equal("i1", Helpers.GetFirst(project.GetItems("i")).EvaluatedInclude);
            Assert.Equal("i1", Helpers.MakeList(project.CreateProjectInstance().GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Add an item to an empty project, where the include is escaped
        /// </summary>
        [Fact]
        public void AddItem_EscapedItemInclude()
        {
            Project project = new Project();

            project.AddItem("i", "i%281%29");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i%281%29"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            List<ProjectItem> items = Helpers.MakeList(project.Items);
            Assert.Equal(1, items.Count);
            Assert.Equal("i", items[0].ItemType);
            Assert.Equal("i(1)", items[0].EvaluatedInclude);
            Assert.Equal("i(1)", Helpers.GetFirst(project.GetItems("i")).EvaluatedInclude);
            Assert.Equal("i(1)", Helpers.MakeList(project.CreateProjectInstance().GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Add an item with metadata
        /// </summary>
        [Fact]
        public void AddItem_WithMetadata()
        {
            Project project = new Project();

            List<KeyValuePair<string, string>> metadata = new List<KeyValuePair<string, string>>();
            metadata.Add(new KeyValuePair<string, string>("m", "m1"));

            project.AddItem("i", "i1", metadata);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Add an item with empty include.
        /// Should throw.
        /// </summary>
        [Fact]
        public void AddItem_InvalidEmptyInclude()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Project project = new Project();

                project.AddItem("i", String.Empty);
            }
           );
        }
        /// <summary>
        /// Add an item with null metadata parameter.
        /// Should just add no metadata.
        /// </summary>
        [Fact]
        public void AddItem_NullMetadata()
        {
            Project project = new Project();

            project.AddItem("i", "i1", null);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Add an item whose include has a property expression. As a convenience, we attempt to expand the
        /// expression to create the evaluated include.
        /// </summary>
        [Fact]
        public void AddItem_IncludeContainsPropertyExpression()
        {
            Project project = new Project();
            project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            project.AddItem("i", "$(p)");

            Assert.Equal("$(p)", Helpers.GetFirst(project.Items).UnevaluatedInclude);
            Assert.Equal("v1", Helpers.GetFirst(project.Items).EvaluatedInclude);
        }

        /// <summary>
        /// Add an item whose include has a wildcard. We attempt to expand the wildcard using the
        /// file system. In this case, we have one entry in the project and two evaluated items.
        /// </summary>
        [Fact]
        public void AddItem_IncludeContainsWildcard()
        {
            string[] paths = null;

            try
            {
                paths = Helpers.CreateFiles("i1.xxx", "i2.xxx");
                string wildcard = Path.Combine(Path.GetDirectoryName(paths[0]), "*.xxx;");

                Project project = new Project();
                project.AddItem("i", wildcard);

                string expected = string.Format(
                    ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""{0}"" />
  </ItemGroup>
</Project>"),
                    wildcard
                );

                List<ProjectItem> items = Helpers.MakeList(project.Items);
                Assert.Equal(2, items.Count);
                Assert.Equal(paths[0], items[0].EvaluatedInclude);
                Assert.Equal(paths[1], items[1].EvaluatedInclude);
            }
            finally
            {
                Helpers.DeleteFiles(paths);
            }
        }

        /// <summary>
        /// Add an item whose include has an item expression. As a convenience, we attempt to expand the
        /// expression to create the evaluated include.
        /// This value will not be reliable until the project is reevaluated --
        /// for example, it assumes any items referenced are defined above this one.
        /// </summary>
        [Fact]
        public void AddItem_IncludeContainsItemExpression()
        {
            Project project = new Project();
            project.AddItem("h", "h1");
            project.ReevaluateIfNecessary();

            ProjectItem item = project.AddItem("i", "@(h)")[0];

            Assert.Equal("@(h)", item.UnevaluatedInclude);
            Assert.Equal("h1", item.EvaluatedInclude);
        }

        /// <summary>
        /// Add an item whose include contains a wildcard but doesn't match anything.
        /// </summary>
        [Fact]
        public void AddItem_ContainingWildcardNoMatches()
        {
            Project project = new Project();
            IList<ProjectItem> items = project.AddItem(
                "i",
                NativeMethodsShared.IsWindows
                    ? @"c:\" + Guid.NewGuid().ToString() + @"\**\i1"
                    : "/" + Guid.NewGuid().ToString() + "/**/i1");

            Assert.Equal(0, items.Count);
        }

        /// <summary>
        /// Add an item whose include contains a wildcard.
        /// In this case we don't try to reuse an existing wildcard expression.
        /// </summary>
        [Fact]
        public void AddItem_ContainingWildcardExistingWildcard()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItem("i", "*.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""*.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item whose include contains a semicolon.
        /// In this case we don't try to reuse an existing wildcard expression.
        /// </summary>
        [Fact]
        public void AddItem_ContainingSemicolonExistingWildcard()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItem("i", "i1.xxx;i2.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""i1.xxx;i2.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// If user tries to add a new item that has the same item name as an existing
        /// wildcarded item, but the wildcard won't pick up the new file, then we
        /// of course have to add the new item.
        /// </summary>
        [Fact]
        public void AddItem_DoesntMatchWildcard()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// When the wildcarded item already in the project file has a Condition
        /// on it, we don't try to match with it when a user tries to add a new
        /// item to the project.
        /// </summary>
        [Fact]
        public void AddItem_MatchesWildcardWithCondition()
        {
            Project project = new Project();
            ProjectItemElement itemElement = project.Xml.AddItem("i", "*.xxx");
            itemElement.Condition = "true";
            project.ReevaluateIfNecessary();

            project.AddItem("i", "i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" Condition=""true"" />
    <i Include=""i1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// When the wildcarded item already in the project file has a Exclude
        /// on it, we don't try to match with it when a user tries to add a new
        /// item to the project.
        /// </summary>
        [Fact]
        public void AddItem_MatchesWildcardWithExclude()
        {
            Project project = new Project();
            ProjectItemElement itemElement = project.Xml.AddItem("i", "*.xxx");
            itemElement.Exclude = "i2.xxx";
            project.ReevaluateIfNecessary();

            project.AddItem("i", "i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" Exclude=""i2.xxx"" />
    <i Include=""i1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard.  In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItem_MatchesWildcard()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 = project.AddItem("i", "i1.xxx")[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(true, object.ReferenceEquals(item1, item2));
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard, except that its item type is different.
        /// In this case, we ignore the existing wildcard.
        /// </summary>
        [Fact]
        public void AddItem_MatchesWildcardButNotItemType()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItem("j", "j1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
  </ItemGroup>
  <ItemGroup>
    <j Include=""j1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a complicated recursive wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard.  In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItem_MatchesComplicatedWildcard()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem(
                "i",
                NativeMethodsShared.IsWindows ? @"c:\subdir1\**\subdir2\**\*.x?x" : "/subdir1/**/subdir2/**/*.x?x");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 =
                project.AddItem(
                    "i",
                    NativeMethodsShared.IsWindows ? @"c:\subdir1\a\b\subdir2\c\i1.xyx" : "/subdir1/a/b/subdir2/c/i1.xyx")
                    [0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
                                  NativeMethodsShared.IsWindows ?
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""c:\subdir1\**\subdir2\**\*.x?x"" />
  </ItemGroup>
</Project>" :
                @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""/subdir1/**/subdir2/**/*.x?x"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(true, object.ReferenceEquals(item1, item2));
        }

        /// <summary>
        /// There's a complicated recursive wildcard in the project already, and the user tries to add an item
        /// that doesn't match that wildcard.  In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItem_DoesntMatchComplicatedWildcard()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", @"c:\subdir1\**\subdir2\**\*.x?x");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 = project.AddItem("i", @"c:\subdir1\a\b\c\i1.xyx")[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""c:\subdir1\**\subdir2\**\*.x?x"" />
    <i Include=""c:\subdir1\a\b\c\i1.xyx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(false, object.ReferenceEquals(item1, item2));
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard.  In this case, we add a new item, because the old
        /// one wasn't equivalent.
        /// In contrast Orcas/Whidbey assumed that the user wants
        /// that metadata on the new item, too.
        /// </summary>
        [Fact]
        public void AddItem_DoesNotMatchWildcardWithMetadata()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", "*.xxx");
            item1.AddMetadata("m", "m1");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 = project.AddItem("i", "i1.xxx")[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"">
      <m>m1</m>
    </i>
    <i Include=""i1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// with metadata.  In this case, we add a new item, because the old
        /// one wasn't equivalent.
        /// </summary>
        [Fact]
        public void AddItemWithMetadata_DoesNotMatchWildcardWithNoMetadata()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            Dictionary<string, string> metadata = new Dictionary<string, string>() { { "m", "m1" } };
            ProjectItemElement item2 = project.AddItem("i", "i1.xxx", metadata)[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""i1.xxx"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a wildcard in the project already, but it's part of a semicolon-separated
        /// list of items.  Now the user tries to add an item that matches that wildcard.
        /// In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItem_MatchesWildcardInSemicolonList()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "a;*.xxx;b");
            project.ReevaluateIfNecessary();

            project.AddItem("i", "i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""a;*.xxx;b"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Modify an item originating in a wildcard by adding a new piece of metadata.
        /// We should blow up the item in the project file.
        /// </summary>
        [Fact]
        public void SetMetadata_ItemOriginatingWithWildcard()
        {
            string[] paths = null;

            try
            {
                paths = Helpers.CreateFiles("i1.xxx", "i2.xxx");
                string directory = Path.GetDirectoryName(paths[0]);
                string wildcard = Path.Combine(directory, "*.xxx;");

                Project project = new Project();
                ProjectItemElement itemElement = project.Xml.AddItem("i", wildcard);
                itemElement.AddMetadata("m", "m0");
                project.ReevaluateIfNecessary();

                Helpers.GetFirst(project.GetItems("i")).SetMetadataValue("n", "n1");

                string expected = string.Format(
                    ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""{0}"">
      <m>m0</m>
      <n>n1</n>
    </i>
    <i Include=""{1}"">
      <m>m0</m>
    </i>
  </ItemGroup>
</Project>"),
                   ProjectCollection.Escape(paths[0]),
                    ProjectCollection.Escape(paths[1]));

                Helpers.VerifyAssertProjectContent(expected, project.Xml);
            }
            finally
            {
                Helpers.DeleteFiles(paths);
            }
        }

        /// <summary>
        /// Set a piece of metadata on an item originating from an item list expression.
        /// We should blow up the expression and set the metadata on one of the resulting items.
        /// </summary>
        [Fact]
        public void SetMetadata_ItemOriginatingWithItemList()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m1</m>
    </h>
    <i Include=""@(h)"" />
  </ItemGroup>
</Project>")));

            Project project = new Project(content);

            Helpers.GetFirst(project.GetItems("i")).SetMetadataValue("m", "m2");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m1</m>
    </h>
    <i Include=""h1"">
      <m>m2</m>
    </i>
    <i Include=""h2"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml, false);
        }

        /// <summary>
        /// Change the value on a piece of metadata on an item originating from an item list expression.
        /// The ProjectMetadata object is shared by all the items here, so the edit does not cause any expansion.
        /// </summary>
        [Fact]
        public void SetMetadataUnevaluatedValue_ItemOriginatingWithItemList()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m1</m>
    </h>
    <i Include=""@(h)"" />
  </ItemGroup>
</Project>")));

            Project project = new Project(content);

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("i")).GetMetadata("m");
            metadatum.UnevaluatedValue = "m2";

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m2</m>
    </h>
    <i Include=""@(h)"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml, false);
        }

        /// <summary>
        /// Modify an item originating in a wildcard by removing a piece of metadata.
        /// We should blow up the item in the project file.
        /// </summary>
        [Fact]
        public void RemoveMetadata_ItemOriginatingWithWildcard()
        {
            string[] paths = null;

            try
            {
                paths = Helpers.CreateFiles("i1.xxx", "i2.xxx");
                string directory = Path.GetDirectoryName(paths[0]);
                string wildcard = Path.Combine(directory, "*.xxx;");

                Project project = new Project();
                ProjectItemElement itemElement = project.Xml.AddItem("i", wildcard);
                itemElement.AddMetadata("m", "m1");
                project.ReevaluateIfNecessary();

                Helpers.GetFirst(project.GetItems("i")).RemoveMetadata("m");

                string expected = string.Format(
                    ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""{0}"" />
    <i Include=""{1}"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>"),
                    ProjectCollection.Escape(paths[0]),
                    ProjectCollection.Escape(paths[1]));

                Helpers.VerifyAssertProjectContent(expected, project.Xml);
            }
            finally
            {
                Helpers.DeleteFiles(paths);
            }
        }

        /// <summary>
        /// There's a wildcard in the project already, but it's part of a semicolon-separated
        /// list of items, and it uses a property reference.  Now the user tries to add a new
        /// item that matches that wildcard.  In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItem_MatchesWildcardWithPropertyReference()
        {
            Project project = new Project();
            project.SetProperty("p", "xxx");
            project.Xml.AddItem("i", "a;*.$(p);b");
            project.ReevaluateIfNecessary();

            project.AddItem("i", "i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>xxx</p>
  </PropertyGroup>
  <ItemGroup>
    <i Include=""a;*.$(p);b"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user renames an item such that it
        /// now matches that wildcard. We don't try to do any thing clever like reuse that wildcard.
        /// </summary>
        [Fact]
        public void RenameItem_MatchesWildcard()
        {
            Project project = new Project();
            project.AddItem("i", "*.xxx");
            project.AddItem("i", "i1");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetLast(project.Items);
            item.Rename("i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""i1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Rename, with the new name containing a property expression.
        /// Because the rename did not cause more items to appear, it is possible
        /// to update the EvaluatedInclude of this one.
        /// </summary>
        [Fact]
        public void RenameItem_NewNameContainsPropertyExpression()
        {
            Project project = new Project();
            project.SetProperty("p", "v1");
            project.AddItem("i", "i1");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetFirst(project.Items);

            item.Rename("$(p)");

            Assert.Equal("$(p)", item.UnevaluatedInclude);

            // Rename should have been expanded in this simple case
            Assert.Equal("v1", item.EvaluatedInclude);

            // The ProjectItemElement should be the same
            ProjectItemElement newItemElement = Helpers.GetFirst((Helpers.GetFirst(project.Xml.ItemGroups)).Items);
            Assert.Equal(true, object.ReferenceEquals(item.Xml, newItemElement));
        }

        /// <summary>
        /// Rename, with the new name containing an item expression.
        /// Because the rename did not cause more items to appear, it is possible
        /// to update the EvaluatedInclude of this one.
        /// </summary>
        [Fact]
        public void RenameItem_NewNameContainsItemExpression()
        {
            Project project = new Project();
            project.SetProperty("p", "v1");
            project.AddItem("h", "h1");
            project.AddItem("i", "i1");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetLast(project.Items);

            item.Rename("@(h)");

            Assert.Equal("@(h)", item.UnevaluatedInclude);

            // Rename should have been expanded in this simple case
            Assert.Equal("h1", item.EvaluatedInclude);

            // The ProjectItemElement should be the same
            ProjectItemElement newItemElement = Helpers.GetLast((Helpers.GetLast(project.Xml.ItemGroups)).Items);
            Assert.Equal(true, object.ReferenceEquals(item.Xml, newItemElement));
        }

        /// <summary>
        /// Rename, with the new name containing an item expression.
        /// Because the new name expands to more than one item, we don't attempt to
        /// update the evaluated include.
        /// </summary>
        [Fact]
        public void RenameItem_NewNameContainsItemExpressionExpandingToTwoItems()
        {
            Project project = new Project();
            project.AddItem("h", "h1");
            project.AddItem("h", "h2");
            project.AddItem("i", "i1");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetLast(project.Items);

            item.Rename("@(h)");

            Assert.Equal("@(h)", item.UnevaluatedInclude);
            Assert.Equal("@(h)", item.EvaluatedInclude);

            // The ProjectItemElement should be the same
            ProjectItemElement newItemElement = Helpers.GetLast((Helpers.GetLast(project.Xml.ItemGroups)).Items);
            Assert.Equal(true, object.ReferenceEquals(item.Xml, newItemElement));
        }

        /// <summary>
        /// Rename, with the new name containing an item expression.
        /// Because the new name expands to not exactly one item, we don't attempt to
        /// update the evaluated include.
        /// Reasoning: The case we interested in for expansion here is setting something
        /// like "$(sourcesroot)\foo.cs" and expanding that to a single item.
        /// If say "@(foo)" is set as the new name, and it expands to blank, that might
        /// be surprising to the host and maybe even unhandled, if on full reevaluation
        /// it wouldn't expand to blank. That's why I'm being cautious and supporting
        /// the most common scenario only. Many hosts will do a ReevaluateIfNecessary before reading anyway (including CPS)
        /// </summary>
        [Fact]
        public void RenameItem_NewNameContainsItemExpressionExpandingToZeroItems()
        {
            Project project = new Project();
            project.AddItem("i", "i1");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetLast(project.Items);

            item.Rename("@(h)");

            Assert.Equal("@(h)", item.UnevaluatedInclude);
            Assert.Equal("@(h)", item.EvaluatedInclude);
        }

        /// <summary>
        /// Rename an item that originated in an expression like "@(h)"
        /// We should blow up the expression and rename the correct part.
        /// </summary>
        [Fact]
        public void RenameItem_OriginatingWithItemList()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m1</m>
    </h>
    <i Include=""@(h)"" />
  </ItemGroup>
</Project>")));

            Project project = new Project(content);

            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));
            item.Rename("h1b");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m1</m>
    </h>
    <i Include=""h1b"">
      <m>m1</m>
    </i>
    <i Include=""h2"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml, false);
        }

        /// <summary>
        /// Rename an item that originated in an expression like "a.cs;b.cs"
        /// We should blow up the expression and rename the correct part.
        /// </summary>
        [Fact]
        public void RenameItem_OriginatingWithSemicolon()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "i1;i2;i3");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.MakeList(project.GetItems("i"))[1];
            item.Rename("i2b");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
    <i Include=""i2b"" />
    <i Include=""i3"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Rename an item that originated in an expression like "a.cs;b.cs"
        /// to a property expression.
        /// We should blow up the expression and rename the correct part,
        /// and because a split had to occur, we should not expand the expression.
        /// </summary>
        [Fact]
        public void RenameItem_OriginatingWithSemicolonToExpandableExpression()
        {
            Project project = new Project();
            project.SetProperty("p", "v1");
            project.Xml.AddItem("i", "i1;i2;i3");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.MakeList(project.GetItems("i"))[1];
            item.Rename("$(p)");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
  <ItemGroup>
    <i Include=""i1"" />
    <i Include=""$(p)"" />
    <i Include=""i3"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            Assert.Equal("$(p)", (Helpers.MakeList(project.Items))[1].EvaluatedInclude);
        }

        /// <summary>
        /// An item originates from a wildcard, and we rename it to something
        /// that no longer matches the wildcard. This should cause the wildcard to be expanded.
        /// </summary>
        [Fact]
        public void RenameItem_NoLongerMatchesWildcard()
        {
            string[] paths = null;

            try
            {
                paths = Helpers.CreateFiles("i1.xxx", "i2.xxx");
                string directory = Path.GetDirectoryName(paths[0]);
                string wildcard = Path.Combine(directory, "*.xxx;");

                Project project = new Project();
                project.Xml.AddItem("i", wildcard);
                project.ReevaluateIfNecessary();

                ProjectItem item = Helpers.GetFirst(project.Items);
                item.Rename("i1.yyy");

                string expected = string.Format(
                    ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1.yyy"" />
    <i Include=""{0}"" />
  </ItemGroup>
</Project>"),
                    ProjectCollection.Escape(Path.Combine(directory, "i2.xxx")));

                Helpers.VerifyAssertProjectContent(expected, project.Xml);
            }
            finally
            {
                Helpers.DeleteFiles(paths);
            }
        }

        /// <summary>
        /// An item originates from a wildcard, and we rename it to something
        /// that still matches the wildcard. This should not modify the project.
        /// </summary>
        [Fact]
        public void RenameItem_StillMatchesWildcard()
        {
            string[] paths = null;

            try
            {
                paths = Helpers.CreateFiles("i1.xxx");
                string directory = Path.GetDirectoryName(paths[0]);
                string wildcard = Path.Combine(directory, "*.xxx;");

                Project project = new Project();
                project.AddItem("i", wildcard);
                project.ReevaluateIfNecessary();

                string before = project.Xml.RawXml;

                ProjectItem item = Helpers.GetFirst(project.Items);
                item.Rename(Path.Combine(directory, "i2.xxx"));

                Helpers.VerifyAssertLineByLine(before, project.Xml.RawXml);
            }
            finally
            {
                Helpers.DeleteFiles(paths);
            }
        }

        [Theory]
        [MemberData(nameof(ItemElementsThatRequireSplitting))]
        [MemberData(nameof(ItemElementsWithGlobsThatRequireSplitting))]
        public void RenameThrowsWhenItemElementSplittingIsDisabled(string projectContents, int itemIndex, SetupProject setupProject)
        {
            AssertDisabledItemSplitting(projectContents, itemIndex, setupProject, (p, i) => {i.Rename("foo");});
        }

        /// <summary>
        /// Change an item type.
        /// </summary>
        [Fact]
        public void ChangeItemType()
        {
            Project project = new Project();
            project.AddItem("i", "i1");

            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));
            item.ItemType = "j";

            Assert.Equal("j", item.ItemType);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <j Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            ProjectItemGroupElement itemGroupElement = Helpers.GetFirst(project.Xml.ItemGroups);
            Assert.Equal(1, Helpers.MakeList(itemGroupElement.Items).Count);
            Assert.Equal(true, object.ReferenceEquals(itemGroupElement, item.Xml.Parent));

            Assert.Equal(1, Helpers.MakeList(project.Items).Count);
            Assert.Equal(1, Helpers.MakeList(project.ItemsIgnoringCondition).Count);

            Assert.Equal(0, Helpers.MakeList(project.GetItems("i")).Count);
            Assert.Equal(0, Helpers.MakeList(project.GetItemsIgnoringCondition("i")).Count);

            Assert.Equal(true, object.ReferenceEquals(item, Helpers.GetFirst(project.GetItems("j"))));
            Assert.Equal(true, object.ReferenceEquals(item, Helpers.GetFirst(project.GetItemsIgnoringCondition("j"))));
            Assert.Equal(true, object.ReferenceEquals(item, Helpers.GetFirst(project.GetItemsByEvaluatedInclude("i1"))));
        }

        /// <summary>
        /// Change an item type; metadata should stay in place
        /// </summary>
        [Fact]
        public void ChangeItemTypeOnItemWithMetadata()
        {
            Project project = new Project();
            ProjectItem item0 = project.AddItem("i", "i1")[0];
            item0.Xml.Exclude = "e";
            ProjectMetadataElement metadatumElement1 = item0.SetMetadataValue("m", "m1").Xml;
            metadatumElement1.Condition = "true";
            item0.SetMetadataValue("n", "n1");

            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));
            item.ItemType = "j";

            Assert.Equal("j", item.ItemType);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <j Include=""i1"" Exclude=""e"">
      <m Condition=""true"">m1</m>
      <n>n1</n>
    </j>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            // Item element identity changed unfortunately, but metadata elements should be the same objects.
            ProjectItemElement itemElement = Helpers.GetFirst(Helpers.GetFirst(project.Xml.ItemGroups).Items);
            Assert.Equal(true, object.ReferenceEquals(itemElement, metadatumElement1.Parent));

            Assert.Equal(2, Helpers.MakeList(itemElement.Metadata).Count);

            Assert.Equal(2 + 15 /* built-in metadata */, item.MetadataCount);
            Assert.Equal("n1", item.GetMetadataValue("n"));

            // Remove one piece of metadata, to hopefully help verify that the DOM is in a good state
            item.RemoveMetadata("m");

            expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <j Include=""i1"" Exclude=""e"">
      <n>n1</n>
    </j>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Change an item type where the item needs blowing up first.
        /// </summary>
        [Fact]
        public void ChangeItemTypeOnItemNeedingSplitting()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "i1;i2");
            project.ReevaluateIfNecessary();

            ProjectItem item = Helpers.GetFirst(project.GetItems("i"));
            item.ItemType = "j";

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <j Include=""i1"" />
    <i Include=""i2"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            ProjectItemGroupElement itemGroupElement = Helpers.GetFirst(project.Xml.ItemGroups);
            Assert.Equal(2, Helpers.MakeList(itemGroupElement.Items).Count);
            Assert.Equal(true, object.ReferenceEquals(itemGroupElement, item.Xml.Parent));
            Assert.Equal(true, object.ReferenceEquals(itemGroupElement, Helpers.GetFirst(project.GetItems("i")).Xml.Parent));
        }

        [Theory]
        [MemberData(nameof(ItemElementsThatRequireSplitting))]
        [MemberData(nameof(ItemElementsWithGlobsThatRequireSplitting))]
        public void ChangeItemTypeThrowsWhenItemElementSplittingIsDisabled(string projectContents, int itemIndex, SetupProject setupProject)
        {
            AssertDisabledItemSplitting(projectContents, itemIndex, setupProject, (p, i) => { i.ItemType = "foo"; });
        }

        /// <summary>
        /// Remove an item, clearing up the empty item group as well
        /// </summary>
        [Fact]
        public void RemoveItem()
        {
            Project project = new Project();
            project.AddItem("i", "i1");
            project.ReevaluateIfNecessary();

            project.RemoveItem(Helpers.GetFirst(project.GetItems("i")));

            string expected = ObjectModelHelpers.CleanupFileContents(@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" />");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            Assert.Equal(0, Helpers.Count(project.Items));
            Assert.Equal(0, Helpers.MakeList(project.CreateProjectInstance().GetItems("i")).Count);
        }

        /// <summary>
        /// Remove an item that originated in an expression like "@(h)"
        /// We should expand the expression to the remaining items, if any.
        /// Metadata should be preserved.
        /// </summary>
        [Fact]
        public void RemoveItem_OriginatingWithItemList()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m1</m>
    </h>
    <i Include=""@(h)"" />
  </ItemGroup>
</Project>")));

            Project project = new Project(content);

            project.RemoveItem(Helpers.GetFirst(project.GetItems("i")));

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1;h2"">
      <m>m1</m>
    </h>
    <i Include=""h2"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml, false);
        }

        /// <summary>
        /// Remove an item that originated in an expression like "a.cs;b.cs"
        /// We should keep the part of the expression that still applies.
        /// </summary>
        [Fact]
        public void RemoveItem_OriginatingWithSemicolon()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "i1;i2");
            project.ReevaluateIfNecessary();

            project.RemoveItem(Helpers.GetFirst(project.GetItems("i")));

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i2"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Remove an item originating from a wildcard
        /// This should cause the wildcard to be expanded to the remaining items, if any.
        /// Expanding the wildcard should preserve the metadata on it.
        /// </summary>
        [Fact]
        public void RemoveItem_OriginatingWithWildcard()
        {
            string[] paths = null;

            try
            {
                paths = Helpers.CreateFiles("i1.xxx", "i2.xxx");
                string directory = Path.GetDirectoryName(paths[0]);
                string wildcard = Path.Combine(directory, "*.xxx;");

                Project project = new Project();
                ProjectItemElement itemElement = project.Xml.AddItem("i", wildcard);
                itemElement.AddMetadata("m", "m1");
                project.ReevaluateIfNecessary();

                ProjectItem item = Helpers.GetFirst(project.Items);
                project.RemoveItem(item);

                string expected = string.Format(
                    ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""{0}"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>"),
                    ProjectCollection.Escape(Path.Combine(directory, "i2.xxx")));

                Helpers.VerifyAssertProjectContent(expected, project.Xml);
            }
            finally
            {
                Helpers.DeleteFiles(paths);
            }
        }

        /// <summary>
        /// Items in certain locations are stored by the project despite having a false condition -- eg for populating the solution explorer.
        /// Removing an item should remove it from this list too.
        /// </summary>
        [Fact]
        public void RemoveItem_IncludingFromIgnoringConditionList()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup Condition=""false"">
    <i Include=""i1"" />
  </ItemGroup>
</Project>")));

            Project project = new Project(content);

            Assert.Equal(0, Helpers.MakeList(project.GetItems("i")).Count);
            List<ProjectItem> itemsIgnoringCondition = Helpers.MakeList(project.GetItemsIgnoringCondition("i"));
            Assert.Equal(1, itemsIgnoringCondition.Count);
            ProjectItem item = itemsIgnoringCondition[0];
            Assert.Equal("i1", item.EvaluatedInclude);

            bool result = project.RemoveItem(item);

            Assert.Equal(false, result); // false as it was not in the regular items collection
            itemsIgnoringCondition = Helpers.MakeList(project.GetItemsIgnoringCondition("i"));
            Assert.Equal(0, itemsIgnoringCondition.Count);
        }

        [Theory]
        [MemberData(nameof(ItemElementsThatRequireSplitting))]
        [MemberData(nameof(ItemElementsWithGlobsThatRequireSplitting))]
        public void RemoveItemThrowsWhenItemElementSplittingIsDisabled(string projectContents, int itemIndex, SetupProject setupProject)
        {
            AssertDisabledItemSplitting(projectContents, itemIndex, setupProject, (p, i) => { p.RemoveItem(i); });
        }

        [Theory]
        [MemberData(nameof(ItemElementsThatRequireSplitting))]
        [MemberData(nameof(ItemElementsWithGlobsThatRequireSplitting))]
        public void RemoveItemsThrowsWhenItemElementSplittingIsDisabled(string projectContents, int itemIndex, SetupProject setupProject)
        {
            AssertDisabledItemSplitting(projectContents, itemIndex, setupProject, (p, i) => { p.RemoveItems(new [] {i}); });
        }

        /// <summary>
        /// Test simple property set with name and value
        /// </summary>
        [Fact]
        public void SetProperty()
        {
            Project project = new Project();
            int environmentPropertyCount = Helpers.MakeList(project.Properties).Count;

            project.SetProperty("p1", "v1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p1>v1</p1>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            Assert.Equal("v1", project.GetPropertyValue("p1"));
            Assert.Equal("v1", project.CreateProjectInstance().GetPropertyValue("p1"));
            Assert.Equal(1, Helpers.Count(project.Properties) - environmentPropertyCount);
        }

        /// <summary>
        /// Test simple property set with name and value, where the value is escaped
        /// </summary>
        [Fact]
        public void SetProperty_EscapedValue()
        {
            Project project = new Project();
            int environmentPropertyCount = Helpers.MakeList(project.Properties).Count;

            project.SetProperty("p1", "v%5E1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p1>v%5E1</p1>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            Assert.Equal("v^1", project.GetPropertyValue("p1"));
            Assert.Equal("v^1", project.CreateProjectInstance().GetPropertyValue("p1"));
            Assert.Equal(1, Helpers.Count(project.Properties) - environmentPropertyCount);
        }

        /// <summary>
        /// Setting a property that originates in an import should not try to edit the property there.
        /// It should set it in the main project file.
        /// </summary>
        [Fact]
        public void SetPropertyOriginatingInImport()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddImport(NativeMethodsShared.IsWindows ?
                "$(msbuildtoolspath)\\microsoft.common.targets" :
                "$(msbuildtoolspath)/Microsoft.Common.targets");
            Project project = new Project(xml);

            // This property certainly exists in that imported file
            project.SetProperty("OutDir", "foo"); // should not throw

            Assert.Equal("foo", project.GetPropertyValue("OutDir"));
            Assert.Equal(1, Helpers.MakeList(xml.Properties).Count);
        }

        /// <summary>
        /// Verify properties are expanded in new property values
        /// </summary>
        [Fact]
        public void SetPropertyWithPropertyExpression()
        {
            Project project = new Project();
            project.SetProperty("p0", "v0");
            project.SetProperty("p1", "$(p0)");

            Assert.Equal("v0", project.GetPropertyValue("p1"));
        }

        /// <summary>
        /// Verify item expressions are not expanded in new property values.
        /// NOTE: They aren't expanded to "blank". It just seems like that, because
        /// when you output them, item expansion happens after property expansion, and
        /// they may evaluate to blank then. (Unless items do exist at that point.)
        /// </summary>
        [Fact]
        public void SetPropertyWithItemExpression()
        {
            Project project = new Project();
            project.AddItem("i", "i1");
            project.SetProperty("p1", "x@(i)x%(m)x");

            Assert.Equal("x@(i)x%(m)x", project.GetPropertyValue("p1"));
        }

        /// <summary>
        /// Setting a property to the same exact unevaluated and evaluated value
        /// should not dirty the project.
        /// (VS seems to do this a lot.)
        /// </summary>
        [Fact]
        public void SetPropertyWithNoChangesShouldNotDirty()
        {
            Project project = new Project();
            project.SetProperty("p", "v1");
            Assert.Equal(true, project.IsDirty);
            project.ReevaluateIfNecessary();

            project.SetProperty("p", "v1");
            Assert.Equal(false, project.IsDirty);
        }

        /// <summary>
        /// Setting an evaluated property after its XML has been removed should
        /// fail.
        /// </summary>
        [Fact]
        public void SetPropertyAfterRemoved()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                var property = project.SetProperty("p", "v1");
                property.Xml.Parent.RemoveAllChildren();
                property.UnevaluatedValue = "v2";
            }
           );
        }
        /// <summary>
        /// Setting an evaluated property after its XML's parent has been removed should
        /// fail.
        /// </summary>
        [Fact]
        public void SetPropertyAfterRemoved2()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                var property = project.SetProperty("p", "v1");
                property.Xml.Parent.Parent.RemoveAllChildren();
                property.UnevaluatedValue = "v2";
            }
           );
        }
        /// <summary>
        /// Setting an evaluated metadatum after its XML has been removed should
        /// fail.
        /// </summary>
        [Fact]
        public void SetMetadatumAfterRemoved()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                var metadatum = project.AddItem("i", "i1")[0].SetMetadataValue("p", "v1");
                metadatum.Xml.Parent.RemoveAllChildren();
                metadatum.UnevaluatedValue = "v2";
            }
           );
        }
        /// <summary>
        /// Changing an item's type after its XML has been removed should
        /// fail.
        /// </summary>
        [Fact]
        public void SetItemTypeAfterRemoved()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                var item = project.AddItem("i", "i1")[0];
                item.Xml.Parent.RemoveAllChildren();
                item.ItemType = "j";
            }
           );
        }
        /// <summary>
        /// Changing an item's type after its XML has been removed should
        /// fail.
        /// </summary>
        [Fact]
        public void RemoveMetadataAfterItemRemoved()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                var item = project.AddItem("i", "i1")[0];
                item.Xml.Parent.RemoveAllChildren();
                item.RemoveMetadata("m");
            }
           );
        }

        [Theory]
        [MemberData(nameof(ItemElementsThatRequireSplitting))]
        public void RemoveMetadataThrowsWhenItemElementSplittingIsDisabled(string projectContents, int itemIndex, SetupProject setupProject)
        {
            AssertDisabledItemSplitting(projectContents, itemIndex, setupProject, (p, i) => { i.RemoveMetadata("bar"); }, "bar");
        }

        [Theory]
        // explode on item coming from glob that expands to one item
        [InlineData(
            @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                <ItemGroup>
                    <a Include=""*.foo"">
                      {0}
                    </a>
                </ItemGroup>
              </Project>",
            0, // operate on first item
            new[] // files that should be captured by the glob
            {
                "a.foo"
            }
            )]
        // explode on item coming from glob that expands to multiple items
        [InlineData(
            @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                <ItemGroup>
                    <a Include=""*.foo"">
                      {0}
                    </a>
                </ItemGroup>
              </Project>",
            0,
            new[]
            {
                "a.foo",
                "b.foo"
            }
            )]
        public void RemoveMetadataThrowsWhenItemElementSplittingIsDisabledAndItemComesFromGlob(string projectContents, int itemIndex, string[] files)
        {
            using (var env = TestEnvironment.Create())
            {
                var testProject = env.CreateTestProjectWithFiles(projectContents, files);
                var projectFile = testProject.ProjectFile;

                AssertDisabledItemSplitting(
                    projectContents,
                    itemIndex,
                    null,
                    (p, i) => { i.RemoveMetadata("bar"); },
                    "bar",
                    p =>
                    {
                        File.WriteAllText(projectFile, p);
                        return new Project(projectFile);
                    });
            }
        }

        /// <summary>
        /// Setting an evaluated metadatum after its XML's parent has been removed should
        /// fail.
        /// </summary>
        [Fact]
        public void SetMetadatumAfterRemoved2()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                var metadatum = project.AddItem("i", "i1")[0].SetMetadataValue("p", "v1");
                metadatum.Xml.Parent.Parent.RemoveAllChildren();
                metadatum.UnevaluatedValue = "v2";
            }
           );
        }
        /// <summary>
        /// Setting an evaluated metadatum after its XML's parent's parent has been removed should
        /// fail.
        /// </summary>
        [Fact]
        public void SetMetadatumAfterRemoved3()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                var metadatum = project.AddItem("i", "i1")[0].SetMetadataValue("p", "v1");
                metadatum.Xml.Parent.Parent.Parent.RemoveAllChildren();
                metadatum.UnevaluatedValue = "v2";
            }
           );
        }

        [Theory]
        [MemberData(nameof(ItemElementsThatRequireSplitting))]
        [MemberData(nameof(ItemElementsWithGlobsThatRequireSplitting))]
        public void SetMetadataThrowsWhenItemElementSplittingIsDisabled(string projectContents, int itemIndex, SetupProject setupProject)
        {
            AssertDisabledItemSplitting(projectContents, itemIndex, setupProject, (p, i) => { i.SetMetadataValue("foo", "bar"); });
        }

        /// <summary>
        /// After removing an appropriate item group's XML without reevaluation an item is added;
        /// it should go in a new one
        /// </summary>
        [Fact]
        public void AddItemAfterAppropriateItemGroupRemoved()
        {
            Project project = new Project();
            project.AddItem("i", "i1");
            project.Xml.ItemGroups.First().Parent.RemoveAllChildren();
            project.AddItem("i", "i2");

            Assert.Equal(1, project.Xml.Items.Count());

            project.ReevaluateIfNecessary();

            Assert.Equal(1, project.Items.Count());
        }

        /// <summary>
        /// Setting a property after an equivalent's XML has been removed without reevaluation, should
        /// still work.
        /// </summary>
        [Fact]
        public void SetNewPropertyAfterEquivalentRemoved()
        {
            Project project = new Project();
            var property = project.SetProperty("p", "v1");
            property.Xml.Parent.RemoveAllChildren();
            project.SetProperty("p", "v2");

            Assert.Equal(1, project.Xml.Properties.Count());

            project.ReevaluateIfNecessary();

            Assert.Equal("v2", project.GetPropertyValue("p"));
        }

        /// <summary>
        /// Setting a property after an equivalent's XML's parent has been removed without reevaluation, should
        /// still work.
        /// </summary>
        [Fact]
        public void SetNewPropertyAfterEquivalentsParentRemoved()
        {
            Project project = new Project();
            var property = project.SetProperty("p", "v1");
            property.Xml.Parent.Parent.RemoveAllChildren();
            project.SetProperty("p", "v2");

            Assert.Equal(1, project.Xml.Properties.Count());

            project.ReevaluateIfNecessary();

            Assert.Equal("v2", project.GetPropertyValue("p"));
        }

        /// <summary>
        /// Test removing a property. Parent empty group should also be removed.
        /// </summary>
        [Fact]
        public void RemoveProperty()
        {
            Project project = new Project();
            int environmentPropertyCount = Helpers.MakeList(project.Properties).Count;

            project.SetProperty("p1", "v1");
            project.ReevaluateIfNecessary();

            project.RemoveProperty(project.GetProperty("p1"));

            string expected = ObjectModelHelpers.CleanupFileContents(@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" />");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            Assert.Equal(null, project.GetProperty("p1"));
            ProjectInstance instance = project.CreateProjectInstance();
            Assert.Equal(String.Empty, instance.GetPropertyValue("p1"));
            Assert.Equal(0, Helpers.Count(project.Properties) - environmentPropertyCount);
        }

        /// <summary>
        /// Test removing a property. Other property should not be disturbed.
        /// </summary>
        [Fact]
        public void RemovePropertyWithSibling()
        {
            Project project = new Project();
            project.SetProperty("p1", "v1");
            project.SetProperty("p2", "v2");
            project.ReevaluateIfNecessary();

            project.RemoveProperty(project.GetProperty("p1"));

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p2>v2</p2>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Add metadata to an existing item
        /// </summary>
        [Fact]
        public void AddMetadata()
        {
            Project project = new Project();

            ProjectItem item = project.AddItem("i", "i1")[0];

            item.SetMetadataValue("m", "m1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            List<ProjectItem> items = Helpers.MakeList(project.Items);
            Assert.Equal("m1", items[0].GetMetadataValue("m"));
            Assert.Equal("m1", Helpers.MakeList(project.CreateProjectInstance().GetItems("i"))[0].GetMetadataValue("m"));
        }

        /// <summary>
        /// Add metadata to an existing item
        /// </summary>
        [Fact]
        public void AddMetadata_EscapedValue()
        {
            Project project = new Project();

            ProjectItem item = project.AddItem("i", "i1")[0];

            item.SetMetadataValue("m", "m1%24%24");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <m>m1%24%24</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            List<ProjectItem> items = Helpers.MakeList(project.Items);
            Assert.Equal("m1$$", items[0].GetMetadataValue("m"));
            Assert.Equal("m1$$", Helpers.MakeList(project.CreateProjectInstance().GetItems("i"))[0].GetMetadataValue("m"));
        }

        /// <summary>
        /// Add metadata to an existing item that has existing metadata with that name.
        /// Should replace it.
        /// </summary>
        [Fact]
        public void AddMetadata_Existing()
        {
            Project project = new Project();

            ProjectItem item = project.AddItem("i", "i1")[0];

            ProjectMetadata metadatum1 = item.SetMetadataValue("m", "m1");
            ProjectMetadata metadatum2 = item.SetMetadataValue("m", "m2");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <m>m2</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            List<ProjectItem> items = Helpers.MakeList(project.Items);
            Assert.Equal("m2", items[0].GetMetadataValue("m"));
            Assert.Equal(true, object.ReferenceEquals(metadatum1, metadatum2));
        }

        /// <summary>
        /// Add an item whose include expands to several items.
        /// Even without reevaluation, we should get two items.
        /// </summary>
        [Fact]
        public void AddItem_ExpandsToSeveral()
        {
            Project project = new Project();
            IList<ProjectItem> items = project.AddItem("i", "a;b");

            Assert.Equal(true, object.ReferenceEquals(items[0].Xml, items[1].Xml));
            Assert.Equal("a;b", items[0].UnevaluatedInclude);

            items = Helpers.MakeList(project.Items);
            Assert.Equal("a", items[0].EvaluatedInclude);
            Assert.Equal("b", items[1].EvaluatedInclude);
        }

        /// <summary>
        /// Add an item expanding to several, with metadata
        /// </summary>
        [Fact]
        public void AddItem_ExpandsToSeveralWithMetadata()
        {
            Project project = new Project();

            List<KeyValuePair<string, string>> metadata = new List<KeyValuePair<string, string>>();
            metadata.Add(new KeyValuePair<string, string>("m", "m1"));

            project.AddItem("i", "i1;i2", metadata);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <m>m1</m>
    </i>
    <i Include=""i2"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Add metadata that would be modified by evaluation.
        /// Should be evaluated on a best-effort basis.
        /// </summary>
        [Fact]
        public void AddMetadata_Reevaluation()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <l>l1</l>
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>")));

            Project project = new Project(content);

            ProjectItem item = Helpers.GetFirst(project.Items);

            ProjectMetadata metadatum = item.SetMetadataValue("m", "%(l)");

            Assert.Equal("l1", item.GetMetadata("m").EvaluatedValue);
            Assert.Equal("%(l)", item.GetMetadata("m").Xml.Value);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <l>l1</l>
      <m>%(l)</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml, false);

            project.ReevaluateIfNecessary();

            item = Helpers.GetFirst(project.Items);

            Assert.Equal("l1", item.GetMetadata("m").EvaluatedValue);
            Assert.Equal("%(l)", item.GetMetadata("m").Xml.Value);
        }

        /// <summary>
        /// Add a new piece of item definition metadatum and update an existing one.
        /// The new piece has to go in an entirely new item definition.
        /// </summary>
        [Fact]
        public void AddMetadatumToItemDefinition()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");
            Project project = new Project(xml);

            ProjectItemDefinition definition = project.ItemDefinitions["i"];
            definition.SetMetadataValue("m", "m1");
            definition.SetMetadataValue("n", "n0");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m>m1</m>
    </i>
    <i>
      <n>n0</n>
    </i>
  </ItemDefinitionGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Add an item to an empty project
        /// </summary>
        [Fact]
        public void AddItemFast()
        {
            Project project = new Project();

            project.AddItemFast("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            List<ProjectItem> items = Helpers.MakeList(project.Items);
            Assert.Equal(1, items.Count);
            Assert.Equal("i", items[0].ItemType);
            Assert.Equal("i1", items[0].EvaluatedInclude);
            Assert.Equal("i1", Helpers.GetFirst(project.GetItems("i")).EvaluatedInclude);
            Assert.Equal("i1", Helpers.MakeList(project.CreateProjectInstance().GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Add an item to an empty project, where the include is escaped
        /// </summary>
        [Fact]
        public void AddItemFast_EscapedItemInclude()
        {
            Project project = new Project();

            project.AddItemFast("i", "i%281%29");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i%281%29"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            List<ProjectItem> items = Helpers.MakeList(project.Items);
            Assert.Equal(1, items.Count);
            Assert.Equal("i", items[0].ItemType);
            Assert.Equal("i(1)", items[0].EvaluatedInclude);
            Assert.Equal("i(1)", Helpers.GetFirst(project.GetItems("i")).EvaluatedInclude);
            Assert.Equal("i(1)", Helpers.MakeList(project.CreateProjectInstance().GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Add an item with metadata
        /// </summary>
        [Fact]
        public void AddItemFast_WithMetadata()
        {
            Project project = new Project();

            List<KeyValuePair<string, string>> metadata = new List<KeyValuePair<string, string>>();
            metadata.Add(new KeyValuePair<string, string>("m", "m1"));

            project.AddItemFast("i", "i1", metadata);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Add an item with empty include.
        /// Should throw.
        /// </summary>
        [Fact]
        public void AddItemFast_InvalidEmptyInclude()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Project project = new Project();

                project.AddItemFast("i", String.Empty);
            }
           );
        }
        /// <summary>
        /// Add an item with null metadata parameter.
        /// Should just add no metadata.
        /// </summary>
        [Fact]
        public void AddItemFast_NullMetadata()
        {
            Project project = new Project();

            project.AddItemFast("i", "i1", null);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Add an item whose include has a property expression. As a convenience, we attempt to expand the
        /// expression to create the evaluated include.
        /// </summary>
        [Fact]
        public void AddItemFast_IncludeContainsPropertyExpression()
        {
            Project project = new Project();
            project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            project.AddItemFast("i", "$(p)");

            Assert.Equal("$(p)", Helpers.GetFirst(project.Items).UnevaluatedInclude);
            Assert.Equal("v1", Helpers.GetFirst(project.Items).EvaluatedInclude);
        }

        /// <summary>
        /// Add an item whose include has a wildcard. We attempt to expand the wildcard using the
        /// file system. In this case, we have one entry in the project and two evaluated items.
        /// </summary>
        [Fact]
        public void AddItemFast_IncludeContainsWildcard()
        {
            string[] paths = null;

            try
            {
                paths = Helpers.CreateFiles("i1.xxx", "i2.xxx");
                string wildcard = Path.Combine(Path.GetDirectoryName(paths[0]), "*.xxx;");

                Project project = new Project();
                project.AddItemFast("i", wildcard);

                string expected = string.Format
                (
                        ObjectModelHelpers.CleanupFileContents(
                            @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                            <ItemGroup>
                                <i Include=""{0}"" />
                            </ItemGroup>
                            </Project>"),
                    wildcard);

                List<ProjectItem> items = Helpers.MakeList(project.Items);
                Assert.Equal(2, items.Count);
                Assert.Equal(paths[0], items[0].EvaluatedInclude);
                Assert.Equal(paths[1], items[1].EvaluatedInclude);
            }
            finally
            {
                Helpers.DeleteFiles(paths);
            }
        }

        /// <summary>
        /// Add an item whose include has an item expression. As a convenience, we attempt to expand the
        /// expression to create the evaluated include.
        /// This value will not be reliable until the project is reevaluated --
        /// for example, it assumes any items referenced are defined above this one.
        /// </summary>
        [Fact]
        public void AddItemFast_IncludeContainsItemExpression()
        {
            Project project = new Project();
            project.AddItemFast("h", "h1");
            project.ReevaluateIfNecessary();

            ProjectItem item = project.AddItemFast("i", "@(h)")[0];

            Assert.Equal("@(h)", item.UnevaluatedInclude);
            Assert.Equal("h1", item.EvaluatedInclude);
        }

        /// <summary>
        /// Add an item whose include contains a wildcard but doesn't match anything.
        /// </summary>
        [Fact]
        public void AddItemFast_ContainingWildcardNoMatches()
        {
            Project project = new Project();
            IList<ProjectItem> items = project.AddItemFast("i",
                NativeMethodsShared.IsWindows ? @"c:\" + Guid.NewGuid().ToString() + @"\**\i1" : "/" + Guid.NewGuid().ToString() + "/**/i1");

            Assert.Equal(0, items.Count);
        }

        /// <summary>
        /// Add an item whose include contains a wildcard.
        /// In this case we don't try to reuse an existing wildcard expression.
        /// </summary>
        [Fact]
        public void AddItemFast_ContainingWildcardExistingWildcard()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItemFast("i", "*.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""*.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item whose include contains a semicolon.
        /// In this case we don't try to reuse an existing wildcard expression.
        /// </summary>
        [Fact]
        public void AddItemFast_ContainingSemicolonExistingWildcard()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItemFast("i", "i1.xxx;i2.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""i1.xxx;i2.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// If user tries to add a new item that has the same item name as an existing
        /// wildcarded item, but the wildcard won't pick up the new file, then we
        /// of course have to add the new item.
        /// </summary>
        [Fact]
        public void AddItemFast_DoesntMatchWildcard()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItemFast("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// When the wildcarded item already in the project file has a Condition
        /// on it, we don't try to match with it when a user tries to add a new
        /// item to the project.
        /// </summary>
        [Fact]
        public void AddItemFast_MatchesWildcardWithCondition()
        {
            Project project = new Project();
            ProjectItemElement itemElement = project.Xml.AddItem("i", "*.xxx");
            itemElement.Condition = "true";
            project.ReevaluateIfNecessary();

            project.AddItemFast("i", "i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" Condition=""true"" />
    <i Include=""i1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// When the wildcarded item already in the project file has a Exclude
        /// on it, we don't try to match with it when a user tries to add a new
        /// item to the project.
        /// </summary>
        [Fact]
        public void AddItemFast_MatchesWildcardWithExclude()
        {
            Project project = new Project();
            ProjectItemElement itemElement = project.Xml.AddItem("i", "*.xxx");
            itemElement.Exclude = "i2.xxx";
            project.ReevaluateIfNecessary();

            project.AddItemFast("i", "i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" Exclude=""i2.xxx"" />
    <i Include=""i1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard.  In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItemFast_MatchesWildcard()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 = project.AddItemFast("i", "i1.xxx")[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(true, object.ReferenceEquals(item1, item2));
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard, except that its item type is different.
        /// In this case, we ignore the existing wildcard.
        /// </summary>
        [Fact]
        public void AddItemFast_MatchesWildcardButNotItemType()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            project.AddItemFast("j", "j1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
  </ItemGroup>
  <ItemGroup>
    <j Include=""j1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a complicated recursive wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard.  In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItemFast_MatchesComplicatedWildcard()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i",
                NativeMethodsShared.IsWindows ? @"c:\subdir1\**\subdir2\**\*.x?x" : "/subdir1/**/subdir2/**/*.x?x");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 = project.AddItemFast("i",
                NativeMethodsShared.IsWindows ? @"c:\subdir1\a\b\subdir2\c\i1.xyx" : "/subdir1/a/b/subdir2/c/i1.xyx")[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
                NativeMethodsShared.IsWindows ?
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""c:\subdir1\**\subdir2\**\*.x?x"" />
  </ItemGroup>
</Project>" :
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""/subdir1/**/subdir2/**/*.x?x"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(true, object.ReferenceEquals(item1, item2));
        }

        /// <summary>
        /// There's a complicated recursive wildcard in the project already, and the user tries to add an item
        /// that doesn't match that wildcard.  In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItemFast_DoesntMatchComplicatedWildcard()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", @"c:\subdir1\**\subdir2\**\*.x?x");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 = project.AddItemFast("i", @"c:\subdir1\a\b\c\i1.xyx")[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""c:\subdir1\**\subdir2\**\*.x?x"" />
    <i Include=""c:\subdir1\a\b\c\i1.xyx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(false, object.ReferenceEquals(item1, item2));
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// that matches that wildcard.  In this case, we add a new item, because the old
        /// one wasn't equivalent.
        /// In contrast Orcas/Whidbey assumed that the user wants
        /// that metadata on the new item, too.
        /// </summary>
        [Fact]
        public void AddItemFast_DoesNotMatchWildcardWithMetadata()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", "*.xxx");
            item1.AddMetadata("m", "m1");
            project.ReevaluateIfNecessary();

            ProjectItemElement item2 = project.AddItemFast("i", "i1.xxx")[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"">
      <m>m1</m>
    </i>
    <i Include=""i1.xxx"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a wildcard in the project already, and the user tries to add an item
        /// with metadata.  In this case, we add a new item, because the old
        /// one wasn't equivalent.
        /// </summary>
        [Fact]
        public void AddItemFastWithMetadata_DoesNotMatchWildcardWithNoMetadata()
        {
            Project project = new Project();
            ProjectItemElement item1 = project.Xml.AddItem("i", "*.xxx");
            project.ReevaluateIfNecessary();

            Dictionary<string, string> metadata = new Dictionary<string, string>() { { "m", "m1" } };
            ProjectItemElement item2 = project.AddItemFast("i", "i1.xxx", metadata)[0].Xml;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""*.xxx"" />
    <i Include=""i1.xxx"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// There's a wildcard in the project already, but it's part of a semicolon-separated
        /// list of items.  Now the user tries to add an item that matches that wildcard.
        /// In this case, we don't touch the project at all.
        /// </summary>
        [Fact]
        public void AddItemFast_MatchesWildcardInSemicolonList()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "a;*.xxx;b");
            project.ReevaluateIfNecessary();

            project.AddItemFast("i", "i1.xxx");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""a;*.xxx;b"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        private static void AssertDisabledItemSplitting(string projectContents, int itemIndex, SetupProject setupProject, Action<Project, ProjectItem> itemOperation, string metadataToInsert = "", Func<string, Project> projectProvider = null)
        {
            var metadataElement = string.IsNullOrEmpty(metadataToInsert)
                ? ""
                : $"<{metadataToInsert}>metadata</{metadataToInsert}>";

            projectContents = string.Format(projectContents, metadataElement);
            projectContents = ObjectModelHelpers.CleanupFileContents(projectContents);

            Project project;

            if (projectProvider != null)
            {
                project = projectProvider(projectContents);
            }
            else
            {
                var content = XmlReader.Create(new StringReader(projectContents));
                project = new Project(content);

                setupProject?.Invoke(project);
                project.ReevaluateIfNecessary();
            }

            project.ThrowInsteadOfSplittingItemElement = true;

            var initialXml = project.Xml.RawXml;
            var item = project.Items.ElementAt(itemIndex);

            var ex = Assert.Throws(typeof(InvalidOperationException), () => itemOperation(project, item));

            Assert.Matches("The requested operation needs to split the item element at location .* into individual elements but item element splitting is disabled with .*", ex.Message);
            Assert.False(project.IsDirty, "project should not be dirty after item splitting threw exception");
            Assert.Equal(initialXml, project.Xml.RawXml);
        }
    }
}
