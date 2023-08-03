// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
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
