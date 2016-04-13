using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.TestFramework;

namespace Microsoft.DotNet.Kestrel.Tests
{
    public class DotnetBuildTest : TestBase
    {
        public static string KestrelPortableApp { get; } = "KestrelPortable";

        [Fact]
        public void BuildingKestrelPortableFatAppProducesExpectedArtifacts()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("KestrelSample")
                .WithLockFiles();

            BuildAndTest(Path.Combine(testInstance.TestRoot, KestrelPortableApp));
        }

        private static void BuildAndTest(string testRoot)
        {
            string appName = Path.GetFileName(testRoot);


            var result = new BuildCommand(
                projectPath: testRoot)
                .ExecuteWithCapturedOutput();

            result.Should().Pass();

            var outputBase = new DirectoryInfo(Path.Combine(testRoot, "bin", "Debug"));

            var netcoreAppOutput = outputBase.Sub("netcoreapp1.0");

            netcoreAppOutput.Should()
                .Exist().And
                .OnlyHaveFiles(new[]
                {
                    $"{appName}.deps.json",
                    $"{appName}.dll",
                    $"{appName}.pdb",
                    $"{appName}.runtimeconfig.json",
                    $"{appName}.runtimeconfig.dev.json"
                });
        }
    }
}
