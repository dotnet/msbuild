// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPublishWithGeneratePackageOnBuildAndPackAsTool : SdkTest
    {
        public GivenThatWeWantToPublishWithGeneratePackageOnBuildAndPackAsTool(ITestOutputHelper log) : base(log)
        {}

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void It_publishes_successfully(bool generatePackageOnBuild, bool packAsTool)
        {
            Console.WriteLine(generatePackageOnBuild.ToString() + packAsTool.ToString());

            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: generatePackageOnBuild.ToString() + packAsTool.ToString())
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "GeneratePackageOnBuild", generatePackageOnBuild.ToString()));
                    propertyGroup.Add(new XElement(ns + "PackAsTool", packAsTool.ToString()));
                });

            var publishCommand = new PublishCommand(testAsset);

            CommandResult result = publishCommand.Execute();

            result.Should()
                  .Pass();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void It_builds_with_GeneratePackageOnBuild_successfully(bool generatePackageOnBuild, bool packAsTool)
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: generatePackageOnBuild.ToString() + packAsTool.ToString())
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "GeneratePackageOnBuild", generatePackageOnBuild.ToString()));
                    propertyGroup.Add(new XElement(ns + "PackAsTool", packAsTool.ToString()));
                });

            var buildCommand = new BuildCommand(testAsset);

            CommandResult result = buildCommand.Execute();

            result.Should()
                  .Pass();
        }
    }
}
