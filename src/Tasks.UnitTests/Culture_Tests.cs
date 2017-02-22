// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class Culture_Tests
    {
        /*
        * Method:   Basic
        *
        * Test the basic functionality.
        */
        [Fact]
        public void Basic()
        {
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo("MyResource.fr.resx", null);
            Assert.Equal("fr", info.culture);
            Assert.Equal("MyResource.resx", info.cultureNeutralFilename);
        }

        /*
        * Method:   NonCultureFile
        *
        * The item doesn't have a culture, and there isn't one embedded in the file name.
        */
        [Fact]
        public void NonCultureFile()
        {
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo("MyResource.resx", null);
            Assert.Equal(null, info.culture);
            Assert.Equal("MyResource.resx", info.cultureNeutralFilename);
        }


        /*
        * Method:   BogusEmbeddedCulture
        *
        * The item has something that looks like an embedded culture, but isn't.
        */
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void BogusEmbeddedCulture()
        {
            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo("MyResource.notalocale.resx", null);
            Assert.Equal(null, info.culture);
            Assert.Equal("MyResource.notalocale.resx", info.cultureNeutralFilename);
        }
    }
}



