// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using Microsoft.Build.Tasks;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class DependentAssembly_Tests
    {
        /// <summary>
        /// Verify that a reference without a public key works correctly
        /// </summary>
        [Fact]
        public void SerializeDeserialize()
        {
            var dependentAssembly = new DependentAssembly();

            string xml = "<assemblyIdentity name='ClassLibrary1'/>";

            dependentAssembly.Read(new XmlTextReader(xml, XmlNodeType.Document, null));

            Assert.NotNull(dependentAssembly.PartialAssemblyName);
        }
    }
}
