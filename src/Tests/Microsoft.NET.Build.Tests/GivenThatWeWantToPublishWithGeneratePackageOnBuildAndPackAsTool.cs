// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Packaging;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToBuildWithGeneratePackageOnBuildAndPackAsTool : SdkTest
    {
        public GivenThatWeWantToBuildWithGeneratePackageOnBuildAndPackAsTool(ITestOutputHelper log) : base(log)
        {}

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void It_builds_successfully(bool generatePackageOnBuild, bool packAsTool)
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
