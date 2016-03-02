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
            var testInstance = TestAssetsManager.CreateTestInstance("BuildTestPortableProject")
                .WithLockFiles();

            var result = new BuildCommand(
                projectPath: testInstance.TestRoot,
                forcePortable: true)
                .ExecuteWithCapturedOutput();

            result.Should().Pass();

            var outputBase = new DirectoryInfo(Path.Combine(testInstance.TestRoot, "bin", "Debug"));

            var netstandardappOutput = outputBase.Sub("netstandardapp1.5");
            var fxSubdirs = new[] {
                netstandardappOutput,
                outputBase.Sub("net45")
            };

            foreach(var fxSubdir in fxSubdirs)
            {
                fxSubdir.Should()
                    .Exist().And
                    .HaveFiles(new[]
                    {
                        "BuildTestPortableProject.dll",
                        "BuildTestPortableProject.pdb"
                    });
            }

            netstandardappOutput.Should().HaveFile("BuildTestPortableProject.deps");
        }
    }
}
