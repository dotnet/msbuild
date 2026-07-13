// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Tasks;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class PropertyParser_Tests
    {
        [MSBuildTestMethod]
        public void GetTable1()
        {
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties", null, out Dictionary<string, string> propertiesTable));

            // We should have null table.
            Assert.IsNull(propertiesTable);
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable3()
        {
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=Debug" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug

            Assert.ContainsSingle(propertiesTable);
            Assert.AreEqual("Debug", propertiesTable["Configuration"]);
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable4()
        {
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=Debug", "Platform=AnyCPU", "VBL=Lab22Dev" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU
            //      VBL                 Lab22Dev

            Assert.AreEqual(3, propertiesTable.Count);
            Assert.AreEqual("Debug", propertiesTable["Configuration"]);
            Assert.AreEqual("AnyCPU", propertiesTable["Platform"]);
            Assert.AreEqual("Lab22Dev", propertiesTable["VBL"]);
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable5()
        {
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration = Debug", "Platform \t=       AnyCPU" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            AnyCPU

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("Debug", propertiesTable["Configuration"]);
            Assert.AreEqual("AnyCPU", propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable6()
        {
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=", "Platform =  " }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       <blank>
            //      Platform            <blank>

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("", propertiesTable["Configuration"]);
            Assert.AreEqual("", propertiesTable["Platform"]);
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable7()
        {
            // This is a failure case.
            Assert.IsFalse(PropertyParser.GetTable(null, "Properties", new[] { "=Debug" }, out _));
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable8()
        {
            // This is a failure case.  (Second property "x86" doesn't have a value.)
            Assert.IsFalse(PropertyParser.GetTable(null, "Properties",
                new[] { "Configuration=Debug", "x86" }, out _));
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable9()
        {
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new[] { "DependsOn = Clean; Build" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          Clean; Build

            Assert.ContainsSingle(propertiesTable);
            Assert.AreEqual("Clean; Build", propertiesTable["DependsOn"]);
        }

        /// <summary>
        /// </summary>
        [MSBuildTestMethod]
        public void GetPropertiesTable10()
        {
            Assert.IsTrue(PropertyParser.GetTable(null, "Properties",
                new[] { "Depends On = CleanBuild" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Depends On          CleanBuild

            Assert.ContainsSingle(propertiesTable);
            Assert.AreEqual("CleanBuild", propertiesTable["Depends On"]);
        }

        [MSBuildTestMethod]
        public void GetPropertiesTableWithEscaping1()
        {
            Assert.IsTrue(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new[] { "Configuration = Debug", "Platform = Any CPU" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      Configuration       Debug
            //      Platform            Any CPU

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("Debug", propertiesTable["Configuration"]);
            Assert.AreEqual("Any CPU", propertiesTable["Platform"]);
        }

        [MSBuildTestMethod]
        public void GetPropertiesTableWithEscaping2()
        {
            Assert.IsTrue(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new[] { "WarningsAsErrors = 1234", "5678", "9999", "Configuration=Debug" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      WarningsAsErrors    1234;5678;9999
            //      Configuration       Debug

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual("1234;5678;9999", propertiesTable["WarningsAsErrors"]);
            Assert.AreEqual("Debug", propertiesTable["Configuration"]);
        }

        [MSBuildTestMethod]
        public void GetPropertiesTableWithEscaping3()
        {
            Assert.IsTrue(PropertyParser.GetTableWithEscaping(null, "Properties", "Properties",
                new[] { @"OutDir=c:\Rajeev;s Stuff\binaries", "Configuration=Debug" }, out Dictionary<string, string> propertiesTable));

            // We should have a table that looks like this:
            //      KEY                 VALUE
            //      =================   =========================
            //      OutDir              c:\Rajeev%3bs Stuff\binaries
            //      Configuration       Debug

            Assert.AreEqual(2, propertiesTable.Count);
            Assert.AreEqual(@"c:\Rajeev%3bs Stuff\binaries", propertiesTable["OutDir"]);
            Assert.AreEqual("Debug", propertiesTable["Configuration"]);
        }
    }
}
