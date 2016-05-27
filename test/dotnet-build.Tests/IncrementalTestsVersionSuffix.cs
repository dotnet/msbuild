using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class IncrementalTestsVersionSuffix : IncrementalTestBase
    {
        [Fact]
        public void TestRebuildWhenVersionSuffixChanged()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestSimpleIncrementalApp")
                .WithLockFiles();

            // Build with Version Suffix 1
            var command = new BuildCommand(testInstance.TestRoot, versionSuffix: "1");
            var result = command.ExecuteWithCapturedOutput();

            // Verify the result
            result.Should().HaveCompiledProject("TestSimpleIncrementalApp", ".NETCoreApp,Version=v1.0");

            // Build with Version Suffix 2
            command = new BuildCommand(testInstance.TestRoot, versionSuffix: "2");
            result = command.ExecuteWithCapturedOutput();

            // Verify the result
            result.Should().HaveCompiledProject("TestSimpleIncrementalApp", ".NETCoreApp,Version=v1.0");
        }
    }
}
