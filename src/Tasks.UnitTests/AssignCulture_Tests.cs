// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class AssignCulture_Tests
    {
        /*
        * Method:   Basic
        *
        * Test the basic functionality.
        */
        [Fact]
        public void Basic()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   CultureAttributePrecedence
        *
        * Any pre-existing Culture attribute on the item is to be ignored
        */
        [Fact]
        public void CultureAttributePrecedence()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "en-GB");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   CultureAttributePrecedenceWithBogusCulture
        *
        * This is really a corner case.
        * If the incoming item has a 'Culture' attribute already, but that culture is invalid,
        * we still overwrite that culture.
        */
        [Fact]
        public void CultureAttributePrecedenceWithBogusCulture()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "invalid");   // Bogus culture.
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }



        /*
        * Method:   AttributeForwarding
        *
        * Make sure that attributes set on input items are forwarded to output items.
        * This applies to every attribute except for the one pointed to by CultureAttribute.
        */
        [Fact]
        public void AttributeForwarding()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("MyAttribute", "My Random String");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("My Random String", t.AssignedFiles[0].GetMetadata("MyAttribute"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }


        /*
        * Method:   NoCulture
        *
        * Test the case where an item has no embedded culture. For example,
        * "MyResource.resx"
        */
        [Fact]
        public void NoCulture()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   NoExtension
        *
        * Test the case where an item has no extension. For example "MyResource".
        */
        [Fact]
        public void NoExtension()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   DoubleDot
        *
        * Test the case where an item has two dots embedded, but otherwise looks
        * like a well-formed item. For example "MyResource..resx".
        */
        [Fact]
        public void DoubleDot()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource..resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource..resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource..resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If an item has a "DependentUpon" who's base name matches exactly, then just assume this
        /// is a resource and form that happen to have an embedded culture. That is, don't assign a 
        /// culture to these.
        /// </summary>
        [Fact]
        public void Regress283991()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("DependentUpon", "MyResourcE.fr.vb");
            t.Files = new ITaskItem[] { i };

            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Empty(t.AssignedFilesWithCulture);
            Assert.Single(t.AssignedFilesWithNoCulture);
        }

        /*
        * Method:   PseudoLocalization
        *
        * Test the usage of Windows Pseudo-Locales
        * https://docs.microsoft.com/en-gb/windows/desktop/Intl/pseudo-locales
        */
        [Theory]
        [InlineData("qps-ploc")]
        [InlineData("qps-plocm")]
        [InlineData("qps-ploca")]
        [InlineData("qps-Latn-x-sh")] // Windows 10+
        public void PseudoLocalization(string culture)
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem($"MyResource.{culture}.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Single(t.AssignedFiles);
            Assert.Single(t.CultureNeutralAssignedFiles);
            Assert.Equal(culture, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal($"MyResource.{culture}.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }
    }
}



