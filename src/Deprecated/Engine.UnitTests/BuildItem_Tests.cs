// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BuildItem_Tests
    {
        [Test]
        public void Basic()
        {
            BuildItem item = new BuildItem("i", "i1");
            Assertion.AssertEquals("i", item.Name);
            Assertion.AssertEquals("i1", item.EvaluatedItemSpec);
            Assertion.AssertEquals("i1", item.FinalItemSpec);
            Assertion.AssertEquals("i1", item.FinalItemSpecEscaped);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidNamespace()
        {
            string content = @"
            <i Include='i1' xmlns='XXX'>
               <m>m1</m>
               <n>n1</n>
            </i>";
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content);
            CreateBuildItemFromXmlDocument(doc);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void MissingInclude()
        {
            string content = @"<i  xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>";
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content);
            BuildItem item = CreateBuildItemFromXmlDocument(doc);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void MissingInclude2()
        {
            string content = @"<i Exclude='x' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>";
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content);
            BuildItem item = CreateBuildItemFromXmlDocument(doc);
        }

        [Test]
        public void Metadata()
        {
            string content = @"
            <i Include='i1' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
               <m>$(p)</m>
               <n>n1</n>
            </i>";
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content);
            BuildItem item = CreateBuildItemFromXmlDocument(doc);
            Assertion.AssertEquals("i", item.Name);
            BuildPropertyGroup properties = new BuildPropertyGroup();
            properties.SetProperty("p", "p1");

            // Evaluated
            Expander expander = new Expander(properties, null, ExpanderOptions.ExpandAll);
            item.EvaluateAllItemMetadata(expander, ParserOptions.AllowPropertiesAndItemLists, null, null);
            Assertion.AssertEquals("p1", item.GetEvaluatedMetadata("m"));

            // Unevaluated
            Assertion.AssertEquals("$(p)", item.GetMetadata("m"));
            Assertion.AssertEquals("n1", item.GetMetadata("n"));

            // All custom metadata
            ArrayList metadataNames = new ArrayList(item.CustomMetadataNames);
            Assertion.Assert(metadataNames.Contains("n"));
            Assertion.Assert(metadataNames.Contains("m"));

            // Custom metadata count only
            Assertion.AssertEquals(2, item.CustomMetadataCount);
            
            // All metadata count
            Assertion.AssertEquals(2 + FileUtilities.ItemSpecModifiers.All.Length, item.MetadataCount);
        }

        [Test]
        public void MetadataIncludesItemDefinitionMetadata()
        {
            // Get an item of type "i" that has an item definition library 
            // for type "i" that has default value "m1" for metadata "m"
            // and has value "n1" for metadata "n"
            BuildItem item = GetXmlBackedItemWithDefinitionLibrary();

            // Evaluated
            Expander expander = new Expander(new BuildPropertyGroup(), null, ExpanderOptions.ExpandAll);
            item.EvaluateAllItemMetadata(expander, ParserOptions.AllowPropertiesAndItemLists, null, null);
            Assertion.AssertEquals("m1", item.GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("n1", item.GetEvaluatedMetadata("n"));

            // Unevaluated
            Assertion.AssertEquals("m1", item.GetMetadata("m"));
            Assertion.AssertEquals("n1", item.GetMetadata("n"));

            // All custom metadata
            List<string> metadataNames = new List<string>((IList<string>)item.CustomMetadataNames);
            Assertion.AssertEquals("n", (string)metadataNames[0]);
            Assertion.AssertEquals("m", (string)metadataNames[1]);

            // Custom metadata count only
            Assertion.AssertEquals(3, item.CustomMetadataCount);

            // All metadata count
            Assertion.AssertEquals(item.CustomMetadataCount + FileUtilities.ItemSpecModifiers.All.Length, item.MetadataCount);
        }

        [Test]
        public void VirtualClone()
        {
            BuildItem item = new BuildItem("i", "i1");
            BuildItem clone = item.Clone();

            Assertion.AssertEquals("i", clone.Name);
            Assertion.AssertEquals("i1", clone.EvaluatedItemSpec);
            Assertion.AssertEquals("i1", clone.FinalItemSpec);
            Assertion.AssertEquals("i1", clone.FinalItemSpecEscaped);
        }

        [Test]
        public void RegularClone()
        {
            BuildItem item = GetXmlBackedItemWithDefinitionLibrary();
            BuildItem clone = item.Clone();

            Assertion.AssertEquals("i", clone.Name);
            Assertion.AssertEquals("i1", clone.EvaluatedItemSpec);
            Assertion.AssertEquals("i1", clone.FinalItemSpec);
            Assertion.AssertEquals("i1", clone.FinalItemSpecEscaped);

            // Make sure the itemdefinitionlibrary is cloned, too
            Assertion.AssertEquals("m1", clone.GetEvaluatedMetadata("m"));
        }

        [Test]
        public void CreateClonedParentedItem()
        {
            BuildItem parent = GetXmlBackedItemWithDefinitionLibrary();
            BuildItem child = new BuildItem("i", "i2");
            child.SetMetadata("n", "n2");

            BuildItem clone = BuildItem.CreateClonedParentedItem(child, parent);

            Assertion.AssertEquals("i", clone.Name);
            Assertion.AssertEquals("i2", clone.EvaluatedItemSpec);
            Assertion.AssertEquals("i2", clone.FinalItemSpec);
            Assertion.AssertEquals("i2", clone.FinalItemSpecEscaped);

            // Make sure the itemdefinitionlibrary is cloned, too
            Assertion.AssertEquals("m1", clone.GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("n1", clone.GetEvaluatedMetadata("n"));
        }

        internal static BuildItem GetXmlBackedItemWithDefinitionLibrary()
        {
            string content = @"<i  xmlns='http://schemas.microsoft.com/developer/msbuild/2003' Include='i1'/>";
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(content);

            XmlElement groupElement = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement itemElement = XmlTestUtilities.AddChildElement(groupElement, "i");
            XmlElement metaElement = XmlTestUtilities.AddChildElementWithInnerText(itemElement, "m", "m1");
            XmlElement metaElement2 = XmlTestUtilities.AddChildElementWithInnerText(itemElement, "o", "o1");

            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(groupElement);
            library.Evaluate(null);

            BuildItem item = new BuildItem((XmlElement)doc.FirstChild, false, library);
            item.SetMetadata("n", "n1");
            return item;
        }
        
        private static BuildItem CreateBuildItemFromXmlDocument(XmlDocument doc)
        {
            ItemDefinitionLibrary itemDefinitionLibrary = new ItemDefinitionLibrary(new Project());
            itemDefinitionLibrary.Evaluate(new BuildPropertyGroup());
            BuildItem item = new BuildItem((XmlElement)doc.FirstChild, false, itemDefinitionLibrary);
            return item;
        }
    }
}
