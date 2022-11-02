// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Reflection;

namespace TestNamespace
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestMSBuildParameters()
        {
            var assemblyInfoVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            Assert.AreEqual("1.2.3", assemblyInfoVersion);
        }
    }
}
