using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;

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

        protected TestInstance CreateTestInstance(string testProjectName, string variation = "", [CallerMemberName] string callingMethod = "")
        {
            return TestAssetsManager.CreateTestInstance(Path.Combine("PerformanceTestProjects", testProjectName), callingMethod + variation)
                .WithLockFiles();
        }

        public void Iterate(Action<PerformanceIterationContext> action, string variation = "", int iterations = 3, [CallerMemberName] string callingMethod = "")
        {
            var fullname = callingMethod + (variation != "" ? "" + variation : "");
            // Heat up iteration
            var context = new PerformanceIterationContext();
            action(context);

            TimeSpan totalTime;
            for (int i = 0; i < iterations; i++)
            {
                context = new PerformanceIterationContext();
                action(context);
                totalTime += context.Stopwatch.Elapsed;
            }
            Reporter.Output.WriteLine($"[RESULT] {callingMethod}-{variation} {totalTime.TotalSeconds/iterations:F} sec/iteration".Bold());
        }

        public class PerformanceIterationContext
        {
            public Stopwatch Stopwatch { get; } = new Stopwatch();

            public void Measure(Action action)
            {
                Stopwatch.Start();
                action();
                Stopwatch.Stop();
            }
        }
    }
}