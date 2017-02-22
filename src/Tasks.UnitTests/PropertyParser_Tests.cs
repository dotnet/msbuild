// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class PropertyParser_Tests
    {
        /// <summary>
        /// </summary>
        [Fact]
        public void GetTable1()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTable(null, "Properties", null, out propertiesTable));

            // We should have null table.
            Assert.Null(propertiesTable);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable3()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=Debug" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug

            Assert.Equal(1, propertiesTable.Count);
            Assert.Equal("Debug", (string)propertiesTable["Configuration"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable4()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=Debug", "Platform=AnyCPU", "VBL=Lab22Dev" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU
            //      VBL                 Lab22Dev

            Assert.Equal(3, propertiesTable.Count);
            Assert.Equal("Debug", (string)propertiesTable["Configuration"]);
            Assert.Equal("AnyCPU", (string)propertiesTable["Platform"]);
            Assert.Equal("Lab22Dev", (string)propertiesTable["VBL"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable5()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration = Debug", "Platform \t=       AnyCPU" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("Debug", (string)propertiesTable["Configuration"]);
            Assert.Equal("AnyCPU", (string)propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable6()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=", "Platform =  " }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       <blank>
            //      Platform            <blank>

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("", (string)propertiesTable["Configuration"]);
            Assert.Equal("", (string)propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable7()
        {
            Hashtable propertiesTable;

            // This is a failure case.
            Assert.False(PropertyParser.GetTable(null, "Properties", new string[] { "=Debug" }, out propertiesTable));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable8()
        {
            Hashtable propertiesTable;

            // This is a failure case.  (Second property "x86" doesn't have a value.)
            Assert.False(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=Debug", "x86" }, out propertiesTable));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable9()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new string[] { "DependsOn = Clean; Build" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          Clean; Build

            Assert.Equal(1, propertiesTable.Count);
            Assert.Equal("Clean; Build", (string)propertiesTable["DependsOn"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable10()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new string[] { "Depends On = CleanBuild" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          CleanBuild

            Assert.Equal(1, propertiesTable.Count);
            Assert.Equal("CleanBuild", (string)propertiesTable["Depends On"]);
        }

        [Fact]
        public void GetPropertiesTableWithEscaping1()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new string[] { "Configuration = Debug", "Platform = Any CPU" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            Any CPU

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("Debug", (string)propertiesTable["Configuration"]);
            Assert.Equal("Any CPU", (string)propertiesTable["Platform"]);
        }

        [Fact]
        public void GetPropertiesTableWithEscaping2()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new string[] { "WarningsAsErrors = 1234", "5678", "9999", "Configuration=Debug" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      WarningsAsErrors    1234;5678;9999
            //      Configuration       Debug

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("1234;5678;9999", (string)propertiesTable["WarningsAsErrors"]);
            Assert.Equal("Debug", (string)propertiesTable["Configuration"]);
        }

        [Fact]
        public void GetPropertiesTableWithEscaping3()
        {
            Hashtable propertiesTable;
            Assert.True(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new string[] { @"OutDir=c:\Rajeev;s Stuff\binaries", "Configuration=Debug" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      OutDir              c:\Rajeev%3bs Stuff\binaries
            //      Configuration       Debug

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal(@"c:\Rajeev%3bs Stuff\binaries", (string)propertiesTable["OutDir"]);
            Assert.Equal("Debug", (string)propertiesTable["Configuration"]);
        }
    }
}
