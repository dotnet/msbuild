// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolSelfContainedProject : SdkTest
    {

        public GivenThatWeWantToPackAToolSelfContainedProject(ITestOutputHelper log) : base(log)
        {

        }

        [Fact]
        public void It_should_fail_with_error_message()
        {
            TestAsset helloWorldAsset = _testAssetsManager
                                        .CopyTestAsset("PortableTool", "PackSelfContainedTool")
                                        .WithSource()
                                        .WithProjectChanges(project =>
                                        {
                                            XNamespace ns = project.Root.Name.Namespace;
                                            XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                                            propertyGroup.Add(new XElement("RuntimeIdentifier", "win-x64"));
                                        })
                                        .Restore(Log);
            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            CommandResult result = packCommand.Execute();
            result.ExitCode.Should().NotBe(0);
            result.StdOut.Should().Contain("Pack as tool does not support self contained.");
        }
    }
}
