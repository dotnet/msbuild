// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class PropertyParser_Tests
    {
        [Fact]
        public void GetTable1()
        {
            Assert.True(PropertyParser.GetTable(null, "Properties", null, out Dictionary<string, string> propertiesTable));

            // We should have null table.
            Assert.Null(propertiesTable);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable3()
        {
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=Debug" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug

            Assert.Equal(1, propertiesTable.Count);
            Assert.Equal("Debug", propertiesTable["Configuration"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable4()
        {
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=Debug", "Platform=AnyCPU", "VBL=Lab22Dev" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU
            //      VBL                 Lab22Dev

            Assert.Equal(3, propertiesTable.Count);
            Assert.Equal("Debug", propertiesTable["Configuration"]);
            Assert.Equal("AnyCPU", propertiesTable["Platform"]);
            Assert.Equal("Lab22Dev", propertiesTable["VBL"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable5()
        {
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration = Debug", "Platform \t=       AnyCPU" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("Debug", propertiesTable["Configuration"]);
            Assert.Equal("AnyCPU", propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable6()
        {
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=", "Platform =  " }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       <blank>
            //      Platform            <blank>

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("", propertiesTable["Configuration"]);
            Assert.Equal("", propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable7()
        {
            // This is a failure case.
            Assert.False(PropertyParser.GetTable(null, "Properties", new[] { "=Debug" }, out _));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable8()
        {
            // This is a failure case.  (Second property "x86" doesn't have a value.)
            Assert.False(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=Debug", "x86" }, out _));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable9()
        {
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new[] { "DependsOn = Clean; Build" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          Clean; Build

            Assert.Equal(1, propertiesTable.Count);
            Assert.Equal("Clean; Build", propertiesTable["DependsOn"]);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void GetPropertiesTable10()
        {
            Assert.True(PropertyParser.GetTable(null, "Properties",
                new[] { "Depends On = CleanBuild" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          CleanBuild

            Assert.Equal(1, propertiesTable.Count);
            Assert.Equal("CleanBuild", propertiesTable["Depends On"]);
        }

        [Fact]
        public void GetPropertiesTableWithEscaping1()
        {
            Assert.True(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new[] { "Configuration = Debug", "Platform = Any CPU" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            Any CPU

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("Debug", propertiesTable["Configuration"]);
            Assert.Equal("Any CPU", propertiesTable["Platform"]);
        }

        [Fact]
        public void GetPropertiesTableWithEscaping2()
        {
            Assert.True(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new[] { "WarningsAsErrors = 1234", "5678", "9999", "Configuration=Debug" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      WarningsAsErrors    1234;5678;9999
            //      Configuration       Debug

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal("1234;5678;9999", propertiesTable["WarningsAsErrors"]);
            Assert.Equal("Debug", propertiesTable["Configuration"]);
        }

        [Fact]
        public void GetPropertiesTableWithEscaping3()
        {
            Assert.True(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new[] { @"OutDir=c:\Rajeev;s Stuff\binaries", "Configuration=Debug" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      OutDir              c:\Rajeev%3bs Stuff\binaries
            //      Configuration       Debug

            Assert.Equal(2, propertiesTable.Count);
            Assert.Equal(@"c:\Rajeev%3bs Stuff\binaries", propertiesTable["OutDir"]);
            Assert.Equal("Debug", propertiesTable["Configuration"]);
        }
    }
}
