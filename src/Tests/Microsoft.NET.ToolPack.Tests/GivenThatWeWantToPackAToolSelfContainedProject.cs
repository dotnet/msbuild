// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;

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
            TestAsset helloWorldAsset = CreateAsset();

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            CommandResult result = packCommand.Execute("--property:SelfContained=true");
            result.ExitCode.Should().NotBe(0);
            result.StdOut.Should().Contain(Strings.PackAsToolCannotSupportSelfContained);
        }

        // Reproduce of https://github.com/dotnet/cli/issues/10607
        [Fact]
        public void It_should_not_fail_on_build()
        {
            TestAsset helloWorldAsset = CreateAsset();

            var packCommand = new BuildCommand(helloWorldAsset);

            CommandResult result = packCommand.Execute("--property:SelfContained=true");
            result.ExitCode.Should().Be(0);
        }

        private TestAsset CreateAsset([CallerMemberName] string callingMethod = "")
        {
            TestAsset helloWorldAsset = _testAssetsManager
                                                    .CopyTestAsset("PortableTool", callingMethod)
                                                    .WithSource()
                                                    .WithProjectChanges(project =>
                                                    {
                                                        XNamespace ns = project.Root.Name.Namespace;
                                                        XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                                                        propertyGroup.Add(new XElement("RuntimeIdentifier", "win-x64"));
                                                    });

            return helloWorldAsset;
        }
    }
}
