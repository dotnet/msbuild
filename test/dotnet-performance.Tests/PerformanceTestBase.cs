using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.TestFramework;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class PerformanceTestBase : TestBase
    {
        protected void Build(string project)
        {
            Run(new BuildCommand(project, buildProfile: false));
        }

        protected void Run(TestCommand command)
        {
            command.Execute().Should().Pass();
        }

        protected void RemoveBin(string project)
        {
            Directory.Delete(Path.Combine(project, "bin"), true);
        }

        protected TestInstance[] CreateTestInstances(string[] testProjectNames, [CallerMemberName] string callingMethod = "")
        {
            return testProjectNames.Select(testProjectName =>
            {
                return CreateTestInstance(testProjectName, callingMethod);
            }).ToArray();
        }

        protected TestInstance CreateTestInstance(string testProjectName, [CallerMemberName] string callingMethod = "")
        {
                return TestAssetsManager.CreateTestInstance(Path.Combine("PerformanceTestProjects", testProjectName), callingMethod)
                     .WithLockFiles();
        }
    }
}