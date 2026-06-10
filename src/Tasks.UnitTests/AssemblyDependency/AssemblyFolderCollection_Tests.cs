// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;
using Microsoft.Build.Shared.AssemblyFoldersFromConfig;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    /// <summary>
    /// Direct tests for the streaming <see cref="AssemblyFolderCollection.Load"/> parser. These run on
    /// every target framework (unlike the RAR-driven AssemblyFoldersFromConfig_Tests, which are net472
    /// only), so they guard the parser - including under the trim/AOT analyzers on net10.0.
    /// </summary>
    public class AssemblyFolderCollection_Tests
    {
        private static AssemblyFolderCollection LoadXml(string xml)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, xml);
                return AssemblyFolderCollection.Load(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ParsesAllFields()
        {
            string xml = @"
<AssemblyFoldersConfig>
  <AssemblyFolders>
    <AssemblyFolder>
      <Name>Test Assemblies</Name>
      <FrameworkVersion>v5.0</FrameworkVersion>
      <Path>C:\some\path</Path>
      <Platform>x64</Platform>
    </AssemblyFolder>
  </AssemblyFolders>
</AssemblyFoldersConfig>";

            AssemblyFolderItem folder = LoadXml(xml).AssemblyFolders.ShouldHaveSingleItem();
            folder.Name.ShouldBe("Test Assemblies");
            folder.FrameworkVersion.ShouldBe("v5.0");
            folder.Path.ShouldBe(@"C:\some\path");
            folder.Platform.ShouldBe("x64");
        }

        [Fact]
        public void OptionalFieldsMayBeOmitted()
        {
            string xml = @"
<AssemblyFoldersConfig>
  <AssemblyFolders>
    <AssemblyFolder>
      <FrameworkVersion>v4.0</FrameworkVersion>
      <Path>C:\folder</Path>
    </AssemblyFolder>
  </AssemblyFolders>
</AssemblyFoldersConfig>";

            AssemblyFolderItem folder = LoadXml(xml).AssemblyFolders.ShouldHaveSingleItem();
            folder.Name.ShouldBeNull();
            folder.FrameworkVersion.ShouldBe("v4.0");
            folder.Path.ShouldBe(@"C:\folder");
            folder.Platform.ShouldBeNull();
        }

        [Fact]
        public void PreservesDocumentOrderAndAllFolders()
        {
            string xml = @"
<AssemblyFoldersConfig>
  <AssemblyFolders>
    <AssemblyFolder><FrameworkVersion>v4.0</FrameworkVersion><Path>C:\one</Path></AssemblyFolder>
    <AssemblyFolder><FrameworkVersion>v4.5</FrameworkVersion><Path>C:\two</Path><Platform>x86</Platform></AssemblyFolder>
    <AssemblyFolder><FrameworkVersion>v5.0</FrameworkVersion><Path>C:\three</Path><Platform>x64</Platform></AssemblyFolder>
  </AssemblyFolders>
</AssemblyFoldersConfig>";

            var folders = LoadXml(xml).AssemblyFolders;
            folders.Count.ShouldBe(3);
            folders[0].Path.ShouldBe(@"C:\one");
            folders[0].Platform.ShouldBeNull();
            folders[1].Path.ShouldBe(@"C:\two");
            folders[1].Platform.ShouldBe("x86");
            folders[2].Path.ShouldBe(@"C:\three");
            folders[2].Platform.ShouldBe("x64");
        }

        [Fact]
        public void ChildElementOrderIsNotSignificant()
        {
            string xml = @"
<AssemblyFoldersConfig>
  <AssemblyFolders>
    <AssemblyFolder>
      <Platform>x86</Platform>
      <Path>C:\folder</Path>
      <FrameworkVersion>v4.5</FrameworkVersion>
      <Name>Reordered</Name>
    </AssemblyFolder>
  </AssemblyFolders>
</AssemblyFoldersConfig>";

            AssemblyFolderItem folder = LoadXml(xml).AssemblyFolders.ShouldHaveSingleItem();
            folder.Name.ShouldBe("Reordered");
            folder.FrameworkVersion.ShouldBe("v4.5");
            folder.Path.ShouldBe(@"C:\folder");
            folder.Platform.ShouldBe("x86");
        }

        [Fact]
        public void IgnoresUnrecognizedElements()
        {
            // <Unknown> carries nested content to confirm the whole subtree is skipped.
            string xml = @"
<AssemblyFoldersConfig>
  <SomeHeader>ignored</SomeHeader>
  <AssemblyFolders>
    <AssemblyFolder>
      <FrameworkVersion>v4.5</FrameworkVersion>
      <Unknown>whatever<Nested attr=""x"" /></Unknown>
      <Path>C:\folder</Path>
    </AssemblyFolder>
  </AssemblyFolders>
</AssemblyFoldersConfig>";

            AssemblyFolderItem folder = LoadXml(xml).AssemblyFolders.ShouldHaveSingleItem();
            folder.FrameworkVersion.ShouldBe("v4.5");
            folder.Path.ShouldBe(@"C:\folder");
        }

        [Fact]
        public void ToleratesXmlDeclarationAndComments()
        {
            string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- leading comment -->
<AssemblyFoldersConfig>
  <!-- inner comment -->
  <AssemblyFolders>
    <AssemblyFolder>
      <FrameworkVersion>v4.5</FrameworkVersion>
      <Path>C:\folder</Path>
    </AssemblyFolder>
  </AssemblyFolders>
</AssemblyFoldersConfig>";

            AssemblyFolderItem folder = LoadXml(xml).AssemblyFolders.ShouldHaveSingleItem();
            folder.FrameworkVersion.ShouldBe("v4.5");
            folder.Path.ShouldBe(@"C:\folder");
        }

        [Fact]
        public void EmptyConfig_ReturnsEmptyCollection() =>
            LoadXml("<AssemblyFoldersConfig />").AssemblyFolders.ShouldBeEmpty();

        [Fact]
        public void EmptyAssemblyFoldersElement_ReturnsEmptyCollection() =>
            LoadXml("<AssemblyFoldersConfig><AssemblyFolders></AssemblyFolders></AssemblyFoldersConfig>").AssemblyFolders.ShouldBeEmpty();

        [Fact]
        public void SelfClosingAssemblyFolder_YieldsItemWithNullFields()
        {
            string xml = "<AssemblyFoldersConfig><AssemblyFolders><AssemblyFolder /></AssemblyFolders></AssemblyFoldersConfig>";

            AssemblyFolderItem folder = LoadXml(xml).AssemblyFolders.ShouldHaveSingleItem();
            folder.Name.ShouldBeNull();
            folder.FrameworkVersion.ShouldBeNull();
            folder.Path.ShouldBeNull();
            folder.Platform.ShouldBeNull();
        }

        [Fact]
        public void MalformedXml_ThrowsXmlException()
        {
            // Mirrors the input used by AssemblyFoldersFromConfig_Tests.AssemblyFoldersFromConfigFileMalformed.
            // The resolver relies on this being an XmlException so it can report the file as malformed.
            Should.Throw<XmlException>(() => LoadXml("<<<>><>!<AssemblyFoldersConfig></AssemblyFoldersConfig>"));
        }
    }
}
