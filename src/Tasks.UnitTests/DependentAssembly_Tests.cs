// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

using Microsoft.Build.Tasks;
using Xunit;

#nullable disable

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
