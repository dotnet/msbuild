// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
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
        * Method:   Aliased
        *
        * Test the usage of aliased locales.
        * List taken from https://github.com/CodingDinosaur/CultureIcuTest#icu-locale-alias-list
        */
        [Theory]
        [InlineData("no")]
        [InlineData("zh-CN")]
        [InlineData("zh-HK")]
        [InlineData("zh-MO")]
        [InlineData("zh-SG")]
        [InlineData("zh-TW")]
        public void Aliased(string culture)
        {
            TestValidCulture(culture);
        }

        [Theory]
        [InlineData("in")]
        [InlineData("in-ID")]
        [InlineData("iw")]
        [InlineData("iw-IL")]
        [InlineData("sh")]
        [InlineData("sh-BA")]
        [InlineData("sh-CS")]
        [InlineData("sh-YU")]
        [InlineData("tl")]
        [InlineData("tl-PH")]
        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        public void AliasedUnavailableOnNetFramework(string culture)
        {
            TestValidCulture(culture);
        }

        [Theory]
        [InlineData("az-AZ")]
        [InlineData("bs-BA")]
        [InlineData("en-NH")]
        [InlineData("en-RH")]
        [InlineData("no-NO")]
        [InlineData("no-NO-NY")]
        [InlineData("pa-IN")]
        [InlineData("pa-PK")]
        [InlineData("shi-MA")]
        [InlineData("sr-BA")]
        [InlineData("sr-CS")]
        [InlineData("sr-Cyrl-CS")]
        [InlineData("sr-Cyrl-YU")]
        [InlineData("sr-Latn-CS")]
        [InlineData("sr-Latn-YU")]
        [InlineData("sr-ME")]
        [InlineData("sr-RS")]
        [InlineData("sr-XK")]
        [InlineData("sr-YU")]
        [InlineData("uz-AF")]
        [InlineData("uz-UZ")]
        [InlineData("vai-LR")]
        [InlineData("yue-CN")]
        [InlineData("yue-HK")]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Mono)]
        public void AliasedUnavailableOnMono(string culture)
        {
            TestValidCulture(culture);
        }

        [Theory]
        [InlineData("ars")]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Mono | TargetFrameworkMonikers.NetFramework)]
        public void AliasedUnavailableOnMonoAndNetFramework(string culture)
        {
            TestValidCulture(culture);
        }

        /*
        * Method:   PseudoLocales
        *
        * Windows-only, see https://docs.microsoft.com/en-gb/windows/desktop/Intl/pseudo-locales
        */
        [Theory]
        [InlineData("qps-ploc")]
        [InlineData("qps-plocm")]
        [InlineData("qps-ploca")]
        [InlineData("qps-Latn-x-sh")] // Windows 10+
        [PlatformSpecific(TestPlatforms.Windows)]
        public void PseudoLocales(string culture)
        {
            TestValidCulture(culture);
        }

        private static void TestValidCulture(string culture)
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem($"MyResource.{culture}.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            var assignedFile = t.AssignedFiles.ShouldHaveSingleItem();
            assignedFile.GetMetadata("Culture").ShouldBe(culture);
            assignedFile.ItemSpec.ShouldBe($"MyResource.{culture}.resx");

            var cultureNeutralFile = t.CultureNeutralAssignedFiles.ShouldHaveSingleItem();
            cultureNeutralFile.ItemSpec.ShouldBe("MyResource.resx");
        }

        /*
        * Method:   InvalidCulture
        *
        * Test for invalid culture (i.e. not known by the operating system)
        */
        [Theory]
        [InlineData("@")]
        [InlineData("\U0001F4A5")]
        [InlineData("xx")]
        [InlineData("xxx")]
        [InlineData("yy")]
        [InlineData("yyy")]
        [InlineData("zz")]
        [InlineData("zzz")]
        public void InvalidCulture(string culture)
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem($"MyResource.{culture}.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            var assignedFile = t.AssignedFiles.ShouldHaveSingleItem();
            assignedFile.GetMetadata("Culture").ShouldBeEmpty();
            assignedFile.GetMetadata("WithCulture").ShouldBe("false");
            assignedFile.ItemSpec.ShouldBe($"MyResource.{culture}.resx");

            var cultureNeutralFile = t.CultureNeutralAssignedFiles.ShouldHaveSingleItem();
            cultureNeutralFile.ItemSpec.ShouldBe($"MyResource.{culture}.resx");
        }
    }
}



