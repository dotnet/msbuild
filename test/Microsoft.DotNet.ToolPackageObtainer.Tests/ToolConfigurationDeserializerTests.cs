// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace Microsoft.DotNet.ToolPackageObtainer.Tests
{
    public class ToolConfigurationDeserializerTests
    {
        [Fact]
        public void GivenXmlPathItShouldGetToolConfiguration()
        {
            ToolConfiguration toolConfiguration = ToolConfigurationDeserializer.Deserialize("DotnetToolsConfigGolden.xml");

            toolConfiguration.CommandName.Should().Be("sayhello");
            toolConfiguration.ToolAssemblyEntryPoint.Should().Be("console.dll");
        }

        [Fact]
        public void GivenMalformedPathItThrows()
        {
            Action a = () => ToolConfigurationDeserializer.Deserialize("DotnetToolsConfigMalformed.xml");
            a.ShouldThrow<ToolConfigurationException>()
                .And.Message.Should()
                .Contain("Failed to retrive tool configuration exception, configuration is malformed xml");
        }

        [Fact]
        public void GivenMissingContentItThrows()
        {
            Action a = () => ToolConfigurationDeserializer.Deserialize("DotnetToolsConfigMissing.xml");
            a.ShouldThrow<ToolConfigurationException>()
                .And.Message.Should()
                .Contain("Configuration content error");
        }

        [Fact]
        public void GivenInvalidCharAsFileNameItThrows()
        {
            Action a = () => new ToolConfiguration("na***me", "my.dll");
            a.ShouldThrow<ArgumentException>()
                .And.Message.Should()
                .Contain("Cannot contain following character");
        }
    }
}
