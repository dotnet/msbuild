// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class PropertyParser_Tests
    {
        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetTable1()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties", null, out propertiesTable));

            // We should have null table.
            Assert.IsNull(propertiesTable);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable3()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=Debug" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug

            Assert.AreEqual(1, propertiesTable.Count);
            Assert.AreEqual("Debug", (string)propertiesTable["Configuration"]);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable4()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=Debug", "Platform=AnyCPU", "VBL=Lab22Dev" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU
            //      VBL                 Lab22Dev

            Assert.AreEqual(3, propertiesTable.Count);
            Assert.AreEqual("Debug", (string)propertiesTable["Configuration"]);
            Assert.AreEqual("AnyCPU", (string)propertiesTable["Platform"]);
            Assert.AreEqual("Lab22Dev", (string)propertiesTable["VBL"]);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable5()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration = Debug", "Platform \t=       AnyCPU" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("Debug", (string)propertiesTable["Configuration"]);
            Assert.AreEqual("AnyCPU", (string)propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable6()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=", "Platform =  " }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       <blank>
            //      Platform            <blank>

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("", (string)propertiesTable["Configuration"]);
            Assert.AreEqual("", (string)propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable7()
        {
            Hashtable propertiesTable;

            // This is a failure case.
            Assert.IsTrue(!PropertyParser.GetTable(null, "Properties", new string[] { "=Debug" }, out propertiesTable));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable8()
        {
            Hashtable propertiesTable;

            // This is a failure case.  (Second property "x86" doesn't have a value.)
            Assert.IsTrue(!PropertyParser.GetTable(null, "Properties",
                new string[] { "Configuration=Debug", "x86" }, out propertiesTable));
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable9()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new string[] { "DependsOn = Clean; Build" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          Clean; Build

            Assert.AreEqual(1, propertiesTable.Count);
            Assert.AreEqual("Clean; Build", (string)propertiesTable["DependsOn"]);
        }

        /// <summary>
        /// </summary>
        [TestMethod]
        public void GetPropertiesTable10()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new string[] { "Depends On = CleanBuild" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          CleanBuild

            Assert.AreEqual(1, propertiesTable.Count);
            Assert.AreEqual("CleanBuild", (string)propertiesTable["Depends On"]);
        }

        [TestMethod]
        public void GetPropertiesTableWithEscaping1()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new string[] { "Configuration = Debug", "Platform = Any CPU" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            Any CPU

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("Debug", (string)propertiesTable["Configuration"]);
            Assert.AreEqual("Any CPU", (string)propertiesTable["Platform"]);
        }

        [TestMethod]
        public void GetPropertiesTableWithEscaping2()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new string[] { "WarningsAsErrors = 1234", "5678", "9999", "Configuration=Debug" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      WarningsAsErrors    1234;5678;9999
            //      Configuration       Debug

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("1234;5678;9999", (string)propertiesTable["WarningsAsErrors"]);
            Assert.AreEqual("Debug", (string)propertiesTable["Configuration"]);
        }

        [TestMethod]
        public void GetPropertiesTableWithEscaping3()
        {
            Hashtable propertiesTable;
            Assert.IsTrue(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new string[] { @"OutDir=c:\Rajeev;s Stuff\binaries", "Configuration=Debug" }, out propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      OutDir              c:\Rajeev%3bs Stuff\binaries
            //      Configuration       Debug

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual(@"c:\Rajeev%3bs Stuff\binaries", (string)propertiesTable["OutDir"]);
            Assert.AreEqual("Debug", (string)propertiesTable["Configuration"]);
        }
    }
}
