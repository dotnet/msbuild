// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishAppWithDependencies : TestBase
    {
        [Fact]
        public void PublishTestAppWithContentPackage()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithContentPackage")
                .WithLockFiles();

            var publishCommand = new PublishCommand(testInstance.TestRoot);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDir = publishCommand.GetOutputDirectory(portable: false);

            publishDir.Should().HaveFiles(new[]
            {
                $"AppWithContentPackage{publishCommand.GetExecutableExtension()}",
                "AppWithContentPackage.dll",
                "AppWithContentPackage.deps.json"
            });

            // these files come from the contentFiles of the SharedContentA dependency
            publishDir
                .Sub("scripts")
                .Should()
                .Exist()
                .And
                .HaveFile("run.cmd");

            publishDir
                .Should()
                .HaveFile("config.xml");
        }
    }
}
