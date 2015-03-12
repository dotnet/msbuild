// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class Culture_Tests
    {
        /*
        * Method:   Basic
        *
        * Test the basic functionality.
        */
        [TestMethod]
        public void Basic()
        {
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo("MyResource.fr.resx", null);
            Assert.AreEqual("fr", info.culture);
            Assert.AreEqual("MyResource.resx", info.cultureNeutralFilename);
        }

        /*
        * Method:   NonCultureFile
        *
        * The item doesn't have a culture, and there isn't one embedded in the file name.
        */
        [TestMethod]
        public void NonCultureFile()
        {
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo("MyResource.resx", null);
            Assert.AreEqual(null, info.culture);
            Assert.AreEqual("MyResource.resx", info.cultureNeutralFilename);
        }


        /*
        * Method:   BogusEmbeddedCulture
        *
        * The item has something that looks like an embedded culture, but isn't.
        */
        [TestMethod]
        public void BogusEmbeddedCulture()
        {
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo("MyResource.notalocale.resx", null);
            Assert.AreEqual(null, info.culture);
            Assert.AreEqual("MyResource.notalocale.resx", info.cultureNeutralFilename);
        }
    }
}



