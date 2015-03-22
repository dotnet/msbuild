// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class DependentAssembly_Tests
    {
        /// <summary>
        /// Verify that a reference without a public key works correctly
        /// </summary>
        [TestMethod]
        public void SerializeDeserialize()
        {
            DependentAssembly dependentAssembly = new DependentAssembly();

            string xml = "<assemblyIdentity name='ClassLibrary1'/>";

            dependentAssembly.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            Assert.IsTrue(dependentAssembly.PartialAssemblyName != null);
        }
    }
}
