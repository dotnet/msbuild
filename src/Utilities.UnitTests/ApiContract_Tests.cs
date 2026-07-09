// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Xml;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the <see cref="ApiContract"/> helper.
    /// </summary>
    public sealed class ApiContract_Tests
    {
        [Fact]
        public void IsContainedApiContractsElementRecognizesMatchingName()
        {
            ApiContract.IsContainedApiContractsElement("ContainedApiContracts").ShouldBeTrue();
        }

        [Theory]
        [InlineData("containedapicontracts")]
        [InlineData("CONTAINEDAPICONTRACTS")]
        [InlineData("ApiContract")]
        [InlineData("SomethingElse")]
        [InlineData("")]
        public void IsContainedApiContractsElementRejectsNonMatchingName(string elementName)
        {
            ApiContract.IsContainedApiContractsElement(elementName).ShouldBeFalse();
        }

        [Fact]
        public void IsVersionedContentElementRecognizesMatchingName()
        {
            ApiContract.IsVersionedContentElement("VersionedContent").ShouldBeTrue();
        }

        [Theory]
        [InlineData("versionedcontent")]
        [InlineData("VERSIONEDCONTENT")]
        [InlineData("ContainedApiContracts")]
        [InlineData("SomethingElse")]
        [InlineData("")]
        public void IsVersionedContentElementRejectsNonMatchingName(string elementName)
        {
            ApiContract.IsVersionedContentElement(elementName).ShouldBeFalse();
        }

        [Fact]
        public void ReadContractsElementPopulatesContracts()
        {
            XmlElement element = CreateElement(@"<ContainedApiContracts>
                                                    <ApiContract name=""System"" version=""1.2.0.4"" />
                                                    <ApiContract name=""Windows.Foundation"" version=""1.0.0.0"" />
                                                 </ContainedApiContracts>");

            List<ApiContract> contracts = new List<ApiContract>();
            ApiContract.ReadContractsElement(element, contracts);

            contracts.Count.ShouldBe(2);
            contracts[0].Name.ShouldBe("System");
            contracts[0].Version.ShouldBe("1.2.0.4");
            contracts[1].Name.ShouldBe("Windows.Foundation");
            contracts[1].Version.ShouldBe("1.0.0.0");
        }

        [Fact]
        public void ReadContractsElementUsesEmptyStringForMissingVersion()
        {
            XmlElement element = CreateElement(@"<ContainedApiContracts>
                                                    <ApiContract name=""System"" />
                                                 </ContainedApiContracts>");

            List<ApiContract> contracts = new List<ApiContract>();
            ApiContract.ReadContractsElement(element, contracts);

            contracts.Count.ShouldBe(1);
            contracts[0].Name.ShouldBe("System");
            contracts[0].Version.ShouldBe(string.Empty);
        }

        [Fact]
        public void ReadContractsElementIgnoresNonApiContractChildren()
        {
            XmlElement element = CreateElement(@"<ContainedApiContracts>
                                                    <ApiContract name=""System"" version=""1.0.0.0"" />
                                                    <SomethingElse name=""Ignored"" version=""9.9.9.9"" />
                                                 </ContainedApiContracts>");

            List<ApiContract> contracts = new List<ApiContract>();
            ApiContract.ReadContractsElement(element, contracts);

            contracts.Count.ShouldBe(1);
            contracts[0].Name.ShouldBe("System");
        }

        [Fact]
        public void ReadContractsElementDoesNothingForNonContainedApiContractsElement()
        {
            XmlElement element = CreateElement(@"<NotContainedApiContracts>
                                                    <ApiContract name=""System"" version=""1.0.0.0"" />
                                                 </NotContainedApiContracts>");

            List<ApiContract> contracts = new List<ApiContract>();
            ApiContract.ReadContractsElement(element, contracts);

            contracts.Count.ShouldBe(0);
        }

        [Fact]
        public void ReadContractsElementDoesNothingForNullElement()
        {
            List<ApiContract> contracts = new List<ApiContract>();
            ApiContract.ReadContractsElement(null, contracts);

            contracts.Count.ShouldBe(0);
        }

        private static XmlElement CreateElement(string xml)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            return document.DocumentElement;
        }
    }
}
