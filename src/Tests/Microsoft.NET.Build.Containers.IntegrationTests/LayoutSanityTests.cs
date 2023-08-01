// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests
{
    public class LayoutSanityTests
    {
        //NOTE: this test requires pre-scripts to be run so the dotnet is dogfood dotnet
        [Fact]
        public void ContainersVersionIsSetInWebSDK()
        {
            Assert.True(ToolsetInfo.TryResolveCommand("dotnet", out string dotnetPath));

            string dotnetDirectory = Path.GetDirectoryName(dotnetPath) ?? throw new InvalidOperationException("dotnet is in unexpected location.");
            string expectedWedSdkPropsPath = Path.Combine(dotnetDirectory, "sdk", TestContext.Current.ToolsetUnderTest.SdkVersion, "Sdks", "Microsoft.NET.Sdk.Web", "Sdk", "Sdk.props");

            Assert.True(File.Exists(expectedWedSdkPropsPath), "Expected to see the Web SDK props in the redist directory");

            string projectFileContent = File.ReadAllText(expectedWedSdkPropsPath);
            XDocument projectXml = XDocument.Parse(projectFileContent);

            XNamespace ns = projectXml.Root?.Name.Namespace ?? throw new InvalidOperationException("XML is empty");
            XElement? actualVersionElement = projectXml.Root.Elements(ns + "PropertyGroup")
                .Where(pg => pg.Elements(ns + "_BuiltInMicrosoftNETBuildContainersVersion").Any())
                .Single()
                .Element(ns + "_BuiltInMicrosoftNETBuildContainersVersion");

            Assert.NotNull(actualVersionElement);
            Assert.NotEmpty(actualVersionElement.Value);
            actualVersionElement.Value.Should().BeOneOf(TestContext.Current.ToolsetUnderTest.SdkVersion, TestContext.Current.ToolsetUnderTest.SdkVersion.Split('-')[0]);
        }
    }
}
