using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildInvalidArgumentsTests : TestBase
    {
        [Fact]
        public void ErrorOccursWhenBuildingPortableProjectToSpecificOutputPathWithoutSpecifyingFramework()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var result = new BuildCommand(
                    projectPath: Path.Combine(testInstance.TestRoot, "PortableApp"),
                    output: Path.Combine(testInstance.TestRoot, "out"))
                .ExecuteWithCapturedOutput();

            result.Should().Fail();
            result.Should().HaveStdErrContaining("When the '--output' option is provided, the '--framework' option must also be provided.");
        }

        [Fact]
        public void ErrorOccursWhenBuildingPortableProjectAndSpecifyingFrameworkThatProjectDoesNotSupport()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var result = new BuildCommand(
                    projectPath: Path.Combine(testInstance.TestRoot, "PortableApp"),
                    output: Path.Combine(testInstance.TestRoot, "out"),
                    framework: "sl40")
                .ExecuteWithCapturedOutput();

            result.Should().Fail();
            result.Should().HaveStdErrContaining("Project does not support framework: Silverlight,Version=v4.0.");
        }

        [Fact]
        public void ErrorOccursWhenBuildingStandaloneProjectToSpecificOutputPathWithoutSpecifyingFramework()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var result = new BuildCommand(
                    projectPath: Path.Combine(testInstance.TestRoot, "StandaloneApp"),
                    output: Path.Combine(testInstance.TestRoot, "out"))
                .ExecuteWithCapturedOutput();

            result.Should().Fail();
            result.Should().HaveStdErrContaining("When the '--output' option is provided, the '--framework' option must also be provided.");
        }
    }
}
