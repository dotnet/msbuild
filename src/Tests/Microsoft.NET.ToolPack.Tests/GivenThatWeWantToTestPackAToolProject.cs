// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Packaging;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToTestPackAToolProject : SdkTest
    {
        public GivenThatWeWantToTestPackAToolProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void When_app_project_reference_a_library_it_flows_to_test_project()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("PortableToolWithTestProject")
                .WithSource();

            testAsset.Restore(Log, "App");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "Test");
            var testCommand = new DotnetCommand(Log, "test", appProjectDirectory);
            testCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("Could not load file or assembly");
        }
    }
}
