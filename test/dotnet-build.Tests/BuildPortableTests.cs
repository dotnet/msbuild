using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildPortableTests : TestBase
    {
        [Fact]
        public void BuildingAPortableProjectProducesDepsFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var result = new BuildCommand(
                projectPath: Path.Combine(testInstance.TestRoot, "PortableApp"))
                .ExecuteWithCapturedOutput();

            result.Should().Pass();

            var outputBase = new DirectoryInfo(Path.Combine(testInstance.TestRoot, "PortableApp", "bin", "Debug"));

            var netstandardappOutput = outputBase.Sub("netstandard1.5");

            netstandardappOutput.Should()
                .Exist().And
                .HaveFiles(new[]
                {
                    "PortableApp.deps",
                    "PortableApp.deps.json",
                    "PortableApp.dll",
                    "PortableApp.pdb"
                });
        }
    }
}
