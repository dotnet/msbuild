// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.Utilities.Unittest
{
    [TestClass]
    public class ApiContract_Tests
    {
        [TestMethod]
        public void ContainedElementHandlesNull()
        {
            bool value = ApiContract.IsContainedApiContractsElement(null);
            Assert.IsFalse(value, "should not be valid and not throw on null");
        }

        [TestMethod]
        public void ReadContractsElementHandlesNullElement()
        {
            ApiContract.ReadContractsElement(null, new List<ApiContract>());
        }

        [TestMethod]
        public void ReadContractsElementBasicRead()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<ContainedApiContracts><ApiContract name='UAP' version='1.0.0.0' /></ContainedApiContracts>");
            List<ApiContract> contracts = new List<ApiContract>();

            ApiContract.ReadContractsElement(document.FirstChild as XmlElement, contracts);
            Assert.AreEqual(1, contracts.Count);
            Assert.AreEqual("UAP", contracts[0].Name);
            Assert.AreEqual("1.0.0.0", contracts[0].Version);
        }

        [TestMethod]
        public void ReadContractsElementNoAttributes()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<ContainedApiContracts><ApiContract/></ContainedApiContracts>");
            List<ApiContract> contracts = new List<ApiContract>();

            ApiContract.ReadContractsElement(document.FirstChild as XmlElement, contracts);
            Assert.AreEqual(1, contracts.Count);
        }

        [TestMethod]
        public void ReadContractsElementNoContent()
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(@"<ContainedApiContracts/>");
            List<ApiContract> contracts = new List<ApiContract>();

            ApiContract.ReadContractsElement(document.FirstChild as XmlElement, contracts);
            Assert.AreEqual(0, contracts.Count);
        }
    }
}
