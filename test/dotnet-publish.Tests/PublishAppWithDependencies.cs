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

        [Fact]
        public void PublishTestAppWithReferencesToResources()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("ResourcesTests")
                .WithLockFiles();

            var projectRoot = Path.Combine(testInstance.TestRoot, "TestApp");

            var publishCommand = new PublishCommand(projectRoot);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDir = publishCommand.GetOutputDirectory(portable: true);

            publishDir.Should().HaveFiles(new[]
            {
                "TestApp.dll",
                "TestApp.deps.json"
            });

            foreach (var culture in new[] { "de", "es", "fr", "it", "ja", "ko", "ru", "zh-Hans", "zh-Hant" })
            {
                var cultureDir = publishDir.Sub(culture);

                // Provided by packages
                cultureDir.Should().HaveFiles(new[] {
                    "Microsoft.Data.Edm.resources.dll",
                    "Microsoft.Data.OData.resources.dll",
                    "System.Spatial.resources.dll"
                });

                // Check for the project-to-project one
                if (culture == "fr")
                {
                    cultureDir.Should().HaveFile("TestLibraryWithResources.resources.dll");
                }
            }
        }
    }
}
