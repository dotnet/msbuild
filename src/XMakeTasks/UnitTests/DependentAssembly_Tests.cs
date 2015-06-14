// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class DependentAssembly_Tests
    {
        /// <summary>
        /// Verify that a reference without a public key works correctly
        /// </summary>
        [Test]
        public void SerializeDeserialize()
        {
            DependentAssembly dependentAssembly = new DependentAssembly();

            string xml = "<assemblyIdentity name='ClassLibrary1'/>";

            dependentAssembly.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            Assert.IsTrue(dependentAssembly.PartialAssemblyName != null);
        }
    }
}
