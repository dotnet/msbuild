// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class ToolConfigurationDeserializerTests
    {
        [Fact]
        public void GivenXmlPathItShouldGetToolConfiguration()
        {
            ToolConfiguration toolConfiguration = ToolConfigurationDeserializer.Deserialize("DotnetToolSettingsGolden.xml");

            toolConfiguration.CommandName.Should().Be("sayhello");
            toolConfiguration.ToolAssemblyEntryPoint.Should().Be("console.dll");
        }

        [Fact]
        public void GivenMalformedPathItThrows()
        {
            Action a = () => ToolConfigurationDeserializer.Deserialize("DotnetToolSettingsMalformed.xml");
            a.Should().Throw<ToolConfigurationException>()
                .And.Message.Should()
                .Contain(string.Format(CommonLocalizableStrings.ToolSettingsInvalidXml, string.Empty));
        }

        [Fact]
        public void GivenMissingContentItThrows()
        {
            Action a = () => ToolConfigurationDeserializer.Deserialize("DotnetToolSettingsMissing.xml");
            a.Should().Throw<ToolConfigurationException>()
                .And.Message.Should()
                .Contain(CommonLocalizableStrings.ToolSettingsMissingCommandName);
        }

        [Fact]
        public void GivenMissingVersionItHasWarningReflectIt()
        {
            ToolConfiguration toolConfiguration = ToolConfigurationDeserializer.Deserialize("DotnetToolSettingsMissingVersion.xml");

            toolConfiguration.Warnings.First().Should().Be(CommonLocalizableStrings.FormatVersionIsMissing);
        }

        [Fact]
        public void GivenMajorHigherVersionItHasWarningReflectIt()
        {
            ToolConfiguration toolConfiguration = ToolConfigurationDeserializer.Deserialize("DotnetToolSettingsMajorHigherVersion.xml");

            toolConfiguration.Warnings.First().Should().Be(CommonLocalizableStrings.FormatVersionIsHigher);
        }

        [Fact]
        public void GivenMinorHigherVersionItHasNoWarning()
        {
            ToolConfiguration toolConfiguration = ToolConfigurationDeserializer.Deserialize("DotnetToolSettingsGolden.xml");

            toolConfiguration.Warnings.Should().BeEmpty();
        }

        [Fact]
        public void GivenInvalidCharAsFileNameItThrows()
        {
            var invalidCommandName = "na\0me";
            Action a = () => new ToolConfiguration(invalidCommandName, "my.dll");
            a.Should().Throw<ToolConfigurationException>()
                .And.Message.Should()
                .Contain(
                    string.Format(
                        CommonLocalizableStrings.ToolSettingsInvalidCommandName,
                        invalidCommandName,
                        string.Join(", ", Path.GetInvalidFileNameChars().Select(c => $"'{c}'"))));
        }

        [Fact]
        public void GivenALeadingDotAsFileNameItThrows()
        {
            var invalidCommandName = ".mytool";
            Action a = () => new ToolConfiguration(invalidCommandName, "my.dll");
            a.Should().Throw<ToolConfigurationException>()
                .And.Message.Should()
                .Contain(string.Format(
                        CommonLocalizableStrings.ToolSettingsInvalidLeadingDotCommandName,
                        invalidCommandName));
        }
    }
}
