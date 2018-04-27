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
using Microsoft.NET.Build.Tasks;
using System;
using System.Collections.Generic;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolTargetingNonSupportedTFM : SdkTest
    {

        public GivenThatWeWantToPackAToolTargetingNonSupportedTFM(ITestOutputHelper log) : base(log)
        {

        }

        [Theory]
        // lower than netcoreapp2.0
        [InlineData("TargetFramework", "netcoreapp2.0", "DotnetToolDoesNotSupportTFMLowerThanNetcoreapp21")]
        [InlineData("TargetFramework", "netcoreapp1.1", "DotnetToolDoesNotSupportTFMLowerThanNetcoreapp21")]
        [InlineData("TargetFrameworks", "netcoreapp2.0;netcoreapp2.1", "DotnetToolDoesNotSupportTFMLowerThanNetcoreapp21")]
        // non netcoreapp
        [InlineData("TargetFramework", "netstandard2.0", "DotnetToolOnlySupportNetcoreapp")]
        public void It_should_fail_with_error_message(string targetFrameworkProperty,
            string targetFramework,
            string expectedErrorResourceName)
        {
            TestAsset helloWorldAsset = _testAssetsManager
                                        .CopyTestAsset("PortableTool", "PackNonSupportedTFM")
                                        .WithSource()
                                        .WithProjectChanges(project =>
                                        {
                                            XNamespace ns = project.Root.Name.Namespace;
                                            XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();

                                            propertyGroup.Element(ns + "TargetFramework").Remove();
                                            propertyGroup.Add(new XElement(ns + targetFrameworkProperty, targetFramework));
                                        });

            helloWorldAsset.Restore(Log);

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            CommandResult result = packCommand.Execute();
            result.ExitCode.Should().NotBe(0);

            // walk around attribute requires static
            string expectedErrorMessage = Strings.ResourceManager.GetString(expectedErrorResourceName);

            result.StdOut.Should().Contain(expectedErrorMessage);
        }

        [WindowsOnlyFact]
        public void It_should_fail_with_error_message_on_fullframework()
        {
            It_should_fail_with_error_message("TargetFramework", "net46", "DotnetToolOnlySupportNetcoreapp");
        }
    }
}
