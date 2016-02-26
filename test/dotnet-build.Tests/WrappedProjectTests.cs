using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class WrappedProjectTests: TestBase
    {
        [Fact]
        public void WrappedProjectFilesResolvedCorrectly()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithWrapperProjectDependency")
                                                .WithBuildArtifacts()
                                                .WithLockFiles();

            var root = testInstance.TestRoot;

            // run compile
            var outputDir = Path.Combine(root, "bin");
            var testProject = ProjectUtils.GetProjectJson(root, "TestApp");
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            new DirectoryInfo(outputDir).Should()
                .HaveFiles(new [] { "TestLibrary.dll", "TestLibrary.pdb" });
        }

    }
}
