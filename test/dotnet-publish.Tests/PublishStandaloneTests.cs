// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.TestFramework;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishStandaloneTests : TestBase
    {
        [Fact]
        public void StandaloneAppDoesNotHaveRuntimeConfigDevJsonFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var publishDir = Publish(testInstance);

            publishDir.Should().NotHaveFile("StandaloneApp.runtimeconfig.dev.json");
        }

        [Fact]
        public void StandaloneAppHasResourceDependency()
        {
            // WindowsAzure.Services brings in en, zh etc. resource DLLs.
            // The host has to be able to find these assemblies from the deps file
            // from the standalone app base under the ietf tag directory.

            var testName = "TestAppWithResourceDeps";
            TestInstance instance =
                TestAssetsManager
                    .CreateTestInstance(testName)
                    .WithLockFiles()
                    .WithBuildArtifacts();

            var publishCommand = new PublishCommand(instance.TestRoot);
            publishCommand.Execute().Should().Pass();

            var publishedDir = publishCommand.GetOutputDirectory();
            var extension = publishCommand.GetExecutableExtension();
            var outputExe = testName + extension;
            publishedDir.Should().HaveFiles(new[] { $"{testName}.dll", outputExe });

            var command = new TestCommand(Path.Combine(publishedDir.FullName, outputExe));
            command.Execute("").Should().ExitWith(0);
        }
        
        private DirectoryInfo Publish(TestInstance testInstance)
        {
            var publishCommand = new PublishCommand(Path.Combine(testInstance.TestRoot, "StandaloneApp"));
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            return publishCommand.GetOutputDirectory(portable: false);
        }
    }
}
