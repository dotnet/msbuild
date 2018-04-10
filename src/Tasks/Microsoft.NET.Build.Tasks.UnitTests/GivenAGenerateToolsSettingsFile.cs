// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateToolsSettingsFile
    {
        private XDocument _generatedDocument = null;
        public GivenAGenerateToolsSettingsFile()
        {
            _generatedDocument = GenerateToolsSettingsFile.GenerateDocument("tool.dll", "mytool");
        }

        [Fact]
        public void It_puts_command_name_in_correct_place_of_the_file()
        {
            _generatedDocument
                .Element("DotNetCliTool")
                .Element("Commands")
                .Element("Command")
                .Attribute("Name")
                .Value
                .Should().Be("mytool");
        }

        [Fact]
        public void It_puts_entryPoint_in_correct_place_of_the_file()
        {
            _generatedDocument
                .Element("DotNetCliTool")
                .Element("Commands")
                .Element("Command")
                .Attribute("EntryPoint")
                .Value
                .Should().Be("tool.dll");
        }

        [Fact]
        public void It_puts_runner_as_dotnet()
        {
            _generatedDocument
                .Element("DotNetCliTool")
                .Element("Commands")
                .Element("Command")
                .Attribute("Runner")
                .Value
                .Should().Be("dotnet");
        }

        [Fact]
        public void It_puts_format_version_in_correct_place_of_the_file()
        {
            _generatedDocument
                .Element("DotNetCliTool")
                .Attribute("Version")
                .Value
                .Should().Be("1");
        }
    }
}
