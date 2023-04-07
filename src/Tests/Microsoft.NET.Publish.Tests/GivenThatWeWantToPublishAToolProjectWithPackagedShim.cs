// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using NuGet.Packaging;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System;
using NuGet.Frameworks;

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
