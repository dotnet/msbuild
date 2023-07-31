// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAToolProjectWithPackagedShim : SdkTest
    {
        private const string _customToolCommandName = "customToolCommandName";

        public GivenThatWeWantToPublishAToolProjectWithPackagedShim(ITestOutputHelper log) : base(log)
        {
        }

        private TestAsset SetupTestAsset([CallerMemberName] string callingMethod = "")
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool", callingMethod)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PackAsToolShimRuntimeIdentifiers", $"win-x64;{ToolsetInfo.LatestMacRuntimeIdentifier}-x64"));
                    propertyGroup.Add(new XElement(ns + "ToolCommandName", _customToolCommandName));
                });

            return helloWorldAsset;
        }

        [Fact]
        public void It_contains_dependencies_shims()
        {
            var testAsset = SetupTestAsset();
            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute();

            publishCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework)
                .Sub("shims")
                .Sub("win-x64")
                .EnumerateFiles().Should().Contain(f => f.Name == _customToolCommandName + ".exe");
        }

        [Fact]
        public void It_contains_dependencies_shims_with_no_build()
        {
            var testAsset = SetupTestAsset();
            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute();

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:NoBuild=true");

            publishCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework)
                .Sub("shims")
                .Sub("win-x64")
                .EnumerateFiles().Should().Contain(f => f.Name == _customToolCommandName + ".exe");
        }
    }
}
