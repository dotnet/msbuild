// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatICareAboutVBApps : TestBase
    {
        private TestAssetInstance _testInstance;

        public GivenThatICareAboutVBApps()
        {
            _testInstance = TestAssets.Get("VBTestApp")
                            .CreateInstance()
                            .WithSourceFiles();

            new RestoreCommand()
                .WithWorkingDirectory(_testInstance.Root)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ICanBuildVBApps()
        {
            new BuildCommand()
                .WithWorkingDirectory(_testInstance.Root)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ICanRunVBApps()
        {
            new RunCommand()
                .WithWorkingDirectory(_testInstance.Root)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ICanPublicAndRunVBApps()
        {
            new PublishCommand()
                .WithWorkingDirectory(_testInstance.Root)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(
                _testInstance.Root.FullName,
                "bin",
                configuration,
                "netcoreapp2.2",
                "publish",
                "VBTestApp.dll");

            new DotnetCommand()
                .ExecuteWithCapturedOutput(outputDll)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }
    }
}
