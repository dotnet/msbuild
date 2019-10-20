// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace Microsoft.DotNet.ToolPackage.Tests
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
            a.ShouldThrow<ToolConfigurationException>()
                .And.Message.Should()
                .Contain(string.Format(CommonLocalizableStrings.ToolSettingsInvalidXml, string.Empty));
        }

        [Fact]
        public void GivenMissingContentItThrows()
        {
            Action a = () => ToolConfigurationDeserializer.Deserialize("DotnetToolSettingsMissing.xml");
            a.ShouldThrow<ToolConfigurationException>()
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
            a.ShouldThrow<ToolConfigurationException>()
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
            a.ShouldThrow<ToolConfigurationException>()
                .And.Message.Should()
                .Contain(string.Format(
                        CommonLocalizableStrings.ToolSettingsInvalidLeadingDotCommandName,
                        invalidCommandName));
        }
    }
}
