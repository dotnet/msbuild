// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenWriteAppConfigWithSupportedRuntimeTask
    {
        [Fact]
        public void It_creates_startup_and_supportedRuntime_node_when_there_is_not_any()
        {
            var doc =
                    new XDocument(
                        new XDeclaration("1.0", "utf-8", "true"),
                        new XElement("configuration"));

            WriteAppConfigWithSupportedRuntime.AddSupportedRuntimeToAppconfig(doc, ".NETFramework", "v4.5.2");

            doc.Element("configuration")
                .Elements("startup")
                .Single().Elements()
                .Should().Contain(e => e.Name.LocalName == "supportedRuntime");
        }

        [Fact]
        public void It_creates_supportedRuntime_node_when_there_is_startup()
        {
            var doc =
                    new XDocument(
                        new XDeclaration("1.0", "utf-8", "true"),
                        new XElement("configuration", new XElement("startup")));

            WriteAppConfigWithSupportedRuntime.AddSupportedRuntimeToAppconfig(doc, ".NETFramework", "v4.5.2");

            doc.Element("configuration")
                .Elements("startup")
                .Single().Elements()
                .Should().Contain(e => e.Name.LocalName == "supportedRuntime");
        }

        [Fact]
        public void It_does_not_change_supportedRuntime_node_when_there_is_supportedRuntime()
        {
            var doc =
                    new XDocument(
                        new XDeclaration("1.0", "utf-8", "true"),
                        new XElement("configuration",
                            new XElement("startup",
                                new XElement("supportedRuntime",
                                    new XAttribute("version", "v4.0"),
                                    new XAttribute("sku", ".NETFramework,Version=v4.7.2")))));

            WriteAppConfigWithSupportedRuntime.AddSupportedRuntimeToAppconfig(doc, ".NETFramework", "v4.6.2");

            XElement supportedRuntime = doc.Element("configuration")
                .Elements("startup")
                .Single().Elements("supportedRuntime")
                .Single();

            supportedRuntime.Should().HaveAttribute("version", "v4.0");
            supportedRuntime.Should().HaveAttribute("sku", ".NETFramework,Version=v4.7.2");
        }

        // intersection of https://docs.microsoft.com/en-us/nuget/reference/target-frameworks
        // and https://docs.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/startup/supportedruntime-element#version
        [Theory]
        [InlineData(".NETFramework", "v1.1", "v1.1.4322")]
        [InlineData(".NETFramework", "v2.0", "v2.0.50727")]
        [InlineData(".NETFramework", "v3.5", "v2.0.50727")]
        public void It_generate_correct_version_and_sku_for_below40(
            string targetFrameworkIdentifier,
            string targetFrameworkVersion,
            string expectedVersion)
        {
            var doc =
                    new XDocument(
                        new XDeclaration("1.0", "utf-8", "true"),
                        new XElement("configuration"));

            WriteAppConfigWithSupportedRuntime.AddSupportedRuntimeToAppconfig(doc, targetFrameworkIdentifier, targetFrameworkVersion);

            XElement supportedRuntime = doc.Element("configuration")
                .Elements("startup")
                .Single().Elements("supportedRuntime")
                .Single();

            supportedRuntime.Should().HaveAttribute("version", expectedVersion);
            supportedRuntime.Attribute("sku").Should().BeNull();
        }

        [Theory]
        [InlineData(".NETFramework", "v4.5", "v4.0", ".NETFramework,Version=v4.5")]
        [InlineData(".NETFramework", "v4.5.1", "v4.0", ".NETFramework,Version=v4.5.1")]
        [InlineData(".NETFramework", "v4.5.2", "v4.0", ".NETFramework,Version=v4.5.2")]
        [InlineData(".NETFramework", "v4.6", "v4.0", ".NETFramework,Version=v4.6")]
        [InlineData(".NETFramework", "v4.6.1", "v4.0", ".NETFramework,Version=v4.6.1")]
        [InlineData(".NETFramework", "v4.6.2", "v4.0", ".NETFramework,Version=v4.6.2")]
        [InlineData(".NETFramework", "v4.7", "v4.0", ".NETFramework,Version=v4.7")]
        [InlineData(".NETFramework", "v4.7.1", "v4.0", ".NETFramework,Version=v4.7.1")]
        [InlineData(".NETFramework", "v4.7.2", "v4.0", ".NETFramework,Version=v4.7.2")]
        public void It_generate_correct_version_and_sku_for_above40(
            string targetFrameworkIdentifier,
            string targetFrameworkVersion,
            string expectedVersion,
            string expectedSku)
        {
            var doc =
                new XDocument(
                    new XDeclaration("1.0", "utf-8", "true"),
                    new XElement("configuration"));

            WriteAppConfigWithSupportedRuntime.AddSupportedRuntimeToAppconfig(doc, targetFrameworkIdentifier, targetFrameworkVersion);

            XElement supportedRuntime = doc.Element("configuration")
                .Elements("startup")
                .Single().Elements("supportedRuntime")
                .Single();

            supportedRuntime.Should().HaveAttribute("version", expectedVersion);
            supportedRuntime.Should().HaveAttribute("sku", expectedSku);
        }

        [Theory]
        [InlineData(".NETFramework", "v4.0", "Client", "v4.0", ".NETFramework,Version=v4.0,Profile=Client")]
        public void It_generate_correct_version_and_sku_and_profile(
            string targetFrameworkIdentifier,
            string targetFrameworkVersion,
            string targetFrameworkProfile,
            string expectedVersion,
            string expectedSku)
        {
            var doc =
                new XDocument(
                    new XDeclaration("1.0", "utf-8", "true"),
                    new XElement("configuration"));

            WriteAppConfigWithSupportedRuntime.AddSupportedRuntimeToAppconfig(doc,
                targetFrameworkIdentifier,
                targetFrameworkVersion,
                targetFrameworkProfile);

            XElement supportedRuntime = doc.Element("configuration")
                .Elements("startup")
                .Single().Elements("supportedRuntime")
                .Single();

            supportedRuntime.Should().HaveAttribute("version", expectedVersion);
            supportedRuntime.Should().HaveAttribute("sku", expectedSku);
        }

        [Theory]
        [InlineData("net999")]
        [InlineData("netstandard20")]
        public void It_does_not_generate_version_and_sku_for_non_supported(string targetframework)
        {
            var targetFrameworkParsed = NuGetFramework.Parse(targetframework);

            var doc =
                new XDocument(
                    new XDeclaration("1.0", "utf-8", "true"),
                    new XElement("configuration"));

            var parsedFramework = NuGetFramework.ParseFolder(targetframework);
            WriteAppConfigWithSupportedRuntime.AddSupportedRuntimeToAppconfig(doc, parsedFramework.Framework, parsedFramework.Version.ToString());

            doc.Element("configuration")
                .Elements("startup").Should().BeNullOrEmpty();
        }
    }
}
