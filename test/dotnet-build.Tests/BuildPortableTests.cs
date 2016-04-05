using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.TestFramework;
using Newtonsoft.Json.Linq;
using FluentAssertions;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildPortableTests : TestBase
    {
        [Fact]
        public void BuildingAPortableProjectProducesDepsJsonFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests").WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            netstandardappOutput.Should().Exist().And.HaveFile("PortableApp.deps.json");
        }

        [Fact]
        public void BuildingAPortableProjectProducesADllFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests").WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            netstandardappOutput.Should().Exist().And.HaveFile("PortableApp.dll");
        }

        [Fact]
        public void BuildingAPortableProjectProducesAPdbFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests").WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            netstandardappOutput.Should().Exist().And.HaveFile("PortableApp.pdb");
        }

        [Fact]
        public void BuildingAPortableProjectProducesARuntimeConfigJsonFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests").WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            netstandardappOutput.Should().Exist().And.HaveFile("PortableApp.runtimeconfig.json");
        }

        [Fact]
        public void RuntimeOptionsGetsCopiedToRuntimeConfigJsonForAPortableApp()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            var runtimeConfigJsonPath = Path.Combine(netstandardappOutput.FullName, "PortableApp.runtimeconfig.json");

            using (var stream = new FileStream(runtimeConfigJsonPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var reader = new StreamReader(stream);

                var rawProject = JObject.Parse(reader.ReadToEnd());
                var runtimeOptions = rawProject["runtimeOptions"];

                runtimeOptions["somethingString"].Value<string>().Should().Be("anything");
                runtimeOptions["somethingBoolean"].Value<bool>().Should().BeTrue();
                runtimeOptions["someArray"].ToObject<string[]>().Should().Contain("one", "two");
                runtimeOptions["someObject"].Value<JObject>()["someProperty"].Value<string>().Should().Be("someValue");
            }
        }

        [Fact]
        public void BuildingAPortableProjectProducesARuntimeConfigDevJsonFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests").WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            netstandardappOutput.Should().Exist().And.HaveFile("PortableApp.runtimeconfig.dev.json");
        }

        private DirectoryInfo Build(TestInstance testInstance)
        {
            var result = new BuildCommand(
                projectPath: Path.Combine(testInstance.TestRoot, "PortableApp"))
                .ExecuteWithCapturedOutput();

            result.Should().Pass();

            var outputBase = new DirectoryInfo(Path.Combine(testInstance.TestRoot, "PortableApp", "bin", "Debug"));

            return outputBase.Sub("netstandard1.5");
        }
    }
}
