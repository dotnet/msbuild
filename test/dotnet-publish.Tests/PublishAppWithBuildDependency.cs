using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel;
using Xunit;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishAppWithBuildDependency : TestBase
    {
        [Fact]
        public void PublishExcludesBuildDependencies()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("AppWithDirectDepAndTypeBuild")
                .WithLockFiles();

            var publishCommand = new PublishCommand(testInstance.TestRoot);
            var publishResult = publishCommand.Execute();
            publishResult.Should().Pass();

            var publishDir = publishCommand.GetOutputDirectory(portable: true);

            publishDir.Should().HaveFiles(new[]
            {
                // This one is directly referenced
                "xunit.core.dll"
            });

            // But these are brought in only by the type:build dependency, and should not be published
            publishDir.Should().NotHaveFiles(new [] {
                "xunit.assert.dll"
            });

            // Check the deps file
            var reader = new DependencyContextJsonReader();
            DependencyContext context;
            using (var file = File.OpenRead(Path.Combine(publishDir.FullName, "AppWithDirectDepAndTypeBuild.deps.json")))
            {
                context = reader.Read(file);
            }

            context.RuntimeLibraries.Should().NotContain(l => string.Equals(l.Name, "xunit.assert"));
            context.CompileLibraries.Should().NotContain(l => string.Equals(l.Name, "xunit.assert"));
        }
    }
}
