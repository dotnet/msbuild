// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishNetFrameworkApp : SdkTest
    {
        public PublishNetFrameworkApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void NetStandardFacadesArePublished()
        {
            var netStandardProject = new TestProject()
            {
                Name = "NetStandardProject",
                TargetFrameworks = "netstandard2.0"
            };

            var testProject = new TestProject()
            {
                TargetFrameworks = "net462",
                IsExe = true
            };
            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute()
                .Should()
                .Pass();

            //  There are close to 100 facades that should be copied, just check for a few of them here
            publishCommand.GetOutputDirectory(testProject.TargetFrameworks)
                .Should()
                .HaveFiles(new[]
                {
                    "netstandard.dll",
                    "System.IO.dll",
                    "System.Runtime.dll"
                })
                .And
                .NotHaveFile("netfx.force.conflicts.dll");
        }
    }
}
