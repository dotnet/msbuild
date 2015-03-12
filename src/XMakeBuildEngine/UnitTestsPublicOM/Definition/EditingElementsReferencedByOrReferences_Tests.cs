//-----------------------------------------------------------------------
// <copyright file="EditingElementsReferencedByOrReferences_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for editing elements that are related to other XML elements</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests around editing elements that are referenced by others or the ones that references others.
    /// </summary>
    [TestClass]
    public class EditingElementsReferencedByOrReferences_Tests
    {
        /// <summary>
        /// Changes the item type on an item used with the at operator.
        /// </summary>
        [TestMethod]
        public void ChangeItemTypeInReferencedItem()
        {
            Project project = GetProject(
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <I Include=""X"" />
    <I Include=""@(I);Y"" />
  </ItemGroup>
</Project>");

            ProjectItem item = project.GetItems("I").Where(i => i.UnevaluatedInclude == "X").First();
            item.ItemType = "J";

            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <J Include=""X"" />
    <I Include=""@(I);Y"" />
  </ItemGroup>
</Project>";

            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            project.ReevaluateIfNecessary();
            IEnumerable<ProjectItem> items = project.GetItems("I");

            Assert.AreEqual(1, items.Count(), "Wrong number of items after changing type");
            Assert.AreEqual("Y", items.First().EvaluatedInclude, "Wrong evaluated include after changing type");
        }

        /// <summary>
        /// Removes an item in a ; separated list. It blows up the list.
        /// </summary>
        [TestMethod]
        public void RemoveItemInList()
        {
            Project project = GetProject(
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <I Include=""X"" />
    <I Include=""@(I);Y;Z"" />
  </ItemGroup>
</Project>");

            ProjectItem item = project.GetItems("I").Where(i => i.EvaluatedInclude == "Y").First();
            project.RemoveItem(item);

            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <I Include=""X"" />
    <I Include=""X"" />
    <I Include=""Z"" />
  </ItemGroup>
</Project>";
            
            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Renames an item in a ; separated list. It blows up the list.
        /// </summary>
        [TestMethod]
        public void RenameItemInList()
        {
            Project project = GetProject(
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <I Include=""X"" />
    <I Include=""@(I);Y"" />
  </ItemGroup>
</Project>");

            ProjectItem item = project.GetItems("I").Where(i => i.EvaluatedInclude == "Y").First();
            item.Rename("Z");

            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <I Include=""X"" />
    <I Include=""X"" />
    <I Include=""Z"" />
  </ItemGroup>
</Project>";

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Removes metadata duplicated in item.
        /// </summary>
        [TestMethod]
        public void RemoveMetadata1()
        {
            Project project = GetProject(
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemDefinitionGroup>
    <I>
      <M>A</M>
    </I>
  </ItemDefinitionGroup>
  <ItemGroup>
    <I Include=""X"">
      <M>%(M);B</M>
      <M>%(M);C</M>
    </I>
    <I Include=""Y"">
      <M>%(M);D</M>
    </I>
  </ItemGroup>
</Project>");

            ProjectItem item1 = project.GetItems("I").Where(i => i.EvaluatedInclude == "X").First();
            Assert.AreEqual("A;B;C", item1.GetMetadataValue("M"), "Invalid metadata at start");

            ProjectItem item2 = project.GetItems("I").Where(i => i.EvaluatedInclude == "Y").First();
            Assert.AreEqual("A;D", item2.GetMetadataValue("M"), "Invalid metadata at start");

            item1.RemoveMetadata("M");
            
            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemDefinitionGroup>
    <I>
      <M>A</M>
    </I>
  </ItemDefinitionGroup>
  <ItemGroup>
    <I Include=""X"">
      <M>%(M);B</M>
    </I>
    <I Include=""Y"">
      <M>%(M);D</M>
    </I>
  </ItemGroup>
</Project>";
            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Removes duplicated metadata and checks evaluation.
        /// </summary>
        [TestMethod]
        public void RemoveMetadata2()
        {
            Project project = GetProject(
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemDefinitionGroup>
    <I>
      <M>A</M>
    </I>
  </ItemDefinitionGroup>
  <ItemGroup>
    <I Include=""X"">
      <M>%(M);B</M>
      <M>%(M);C</M>
    </I>
    <I Include=""Y"">
      <M>%(M);D</M>
    </I>
  </ItemGroup>
</Project>");

            ProjectItem item1 = project.GetItems("I").Where(i => i.EvaluatedInclude == "X").First();
            item1.RemoveMetadata("M");
            
            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemDefinitionGroup>
    <I>
      <M>A</M>
    </I>
  </ItemDefinitionGroup>
  <ItemGroup>
    <I Include=""X"">
      <M>%(M);B</M>
    </I>
    <I Include=""Y"">
      <M>%(M);D</M>
    </I>
  </ItemGroup>
</Project>";
            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            project.ReevaluateIfNecessary();
            item1 = project.GetItems("I").Where(i => i.EvaluatedInclude == "X").First();
            Assert.AreEqual("A;B", item1.GetMetadataValue("M"), "Invalid metadata after first removal");
            ProjectItem item2 = project.GetItems("I").Where(i => i.EvaluatedInclude == "Y").First();
            Assert.AreEqual("A;D", item2.GetMetadataValue("M"), "Invalid metadata after first removal");
        }
    
        /// <summary>
        /// Removes metadata but still keep inherited one from item definition.
        /// </summary>
        [TestMethod]
        public void RemoveMetadata3()
        {
            Project project = GetProject(
        @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemDefinitionGroup>
    <I>
      <M>A</M>
    </I>
  </ItemDefinitionGroup>
  <ItemGroup>
    <I Include=""X"">
      <M>%(M);B</M>
      <M>%(M);C</M>
    </I>
    <I Include=""Y"">
      <M>%(M);D</M>
    </I>
  </ItemGroup>
</Project>");

            ProjectItem item1 = project.GetItems("I").Where(i => i.EvaluatedInclude == "X").First();
            item1.RemoveMetadata("M");

            project.ReevaluateIfNecessary();
            ProjectItem item2 = project.GetItems("I").Where(i => i.EvaluatedInclude == "Y").First();
            item2.RemoveMetadata("M");

            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemDefinitionGroup>
    <I>
      <M>A</M>
    </I>
  </ItemDefinitionGroup>
  <ItemGroup>
    <I Include=""X"">
      <M>%(M);B</M>
    </I>
    <I Include=""Y"" />
  </ItemGroup>
</Project>";
            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            project.ReevaluateIfNecessary();
            item1 = project.GetItems("I").Where(i => i.EvaluatedInclude == "X").First();
            Assert.AreEqual("A;B", item1.GetMetadataValue("M"), "Invalid metadata after second removal");
            item2 = project.GetItems("I").Where(i => i.EvaluatedInclude == "Y").First();
            Assert.AreEqual("A", item2.GetMetadataValue("M"), "Invalid metadata after second removal");
        }

        /// <summary>
        /// Removes metadata referenced with % qualification.
        /// </summary>
        [TestMethod]
        public void RemoveReferencedMetadata()
        {
            Project project = GetProject(
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <I Include=""i"">
      <M>m</M>
      <N>%(I.M)</N>
    </I>
  </ItemGroup>
</Project>");

            ProjectItem item = project.GetItems("I").First();
            Assert.AreEqual("m", item.GetMetadataValue("N"), "Wrong metadata value at startup");

            item.RemoveMetadata("M");
            
            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <I Include=""i"">
      <N>%(I.M)</N>
    </I>
  </ItemGroup>
</Project>";
            Helpers.VerifyAssertProjectContent(expected, project.Xml);

            project.ReevaluateIfNecessary();
            item = project.GetItems("I").First();
            ProjectMetadata metadata = item.GetMetadata("N");

            Assert.AreEqual("%(I.M)", metadata.UnevaluatedValue, "Unevaluated value is wrong");
            Assert.AreEqual(String.Empty, metadata.EvaluatedValue, "Evaluated value is wrong");
        }
        
        /// <summary>
        /// Removes duplicated property.
        /// </summary>
        [TestMethod]
        public void RemoveProperty()
        {
            Project project = GetProject(
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <P>A</P>
    <P>$(P)B</P>
  </PropertyGroup>
</Project>");

            ProjectProperty property = project.GetProperty("P");
            project.RemoveProperty(property);

            string expected =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <P>A</P>
  </PropertyGroup>
</Project>";

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Creates a new project the given contents.
        /// </summary>
        /// <param name="contents">The contents for the project.</param>
        /// <returns>The project contents.</returns>
        private Project GetProject(string contents)
        {
            return new Project(XmlReader.Create(new StringReader(contents)));
        }
    }
}
