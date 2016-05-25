// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Xunit.Performance;

namespace Microsoft.DotNet.Tests.Performance
{
    public class HelloWorld : TestBase
    {
        private static readonly string s_testdirName = "helloworldtestroot";
        private static readonly string s_outputdirName = "test space/bin";

        private static string AssetsRoot { get; set; }
        private static string RestoredTestProjectDirectory { get; set; }

        private string TestDirectory { get; set; }
        private string TestProject { get; set; }
        private string OutputDirectory { get; set; }

        static HelloWorld()
        {
            HelloWorld.SetupStaticTestProject();
        }

        public HelloWorld()
        {
        }

        [Benchmark]
        public void MeasureDotNetBuild()
        {
            foreach (var iter in Benchmark.Iterations)
            {
                // Setup a new instance of the test project.
                TestInstanceSetup();

                // Setup the build command.
                var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, framework: DefaultFramework);
                using (iter.StartMeasurement())
                {
                    // Execute the build command.
                    buildCommand.Execute();
                }
            }
        }

        private void TestInstanceSetup()
        {
            var root = Temp.CreateDirectory();

            var testInstanceDir = root.CopyDirectory(RestoredTestProjectDirectory);

            TestDirectory = testInstanceDir.Path;
            TestProject = Path.Combine(TestDirectory, "project.json");
            OutputDirectory = Path.Combine(TestDirectory, s_outputdirName);
        }

        private static void SetupStaticTestProject()
        {
            AssetsRoot = Path.Combine(AppContext.BaseDirectory, "bin");
            RestoredTestProjectDirectory = Path.Combine(AssetsRoot, s_testdirName);

            // Ignore Delete Failure
            try
            {
                Directory.Delete(RestoredTestProjectDirectory, true);
            }
            catch (Exception) { }

            Directory.CreateDirectory(RestoredTestProjectDirectory);

            // Todo: this is a hack until corefx is on nuget.org remove this After RC 2 Release
            NuGetConfig.Write(RestoredTestProjectDirectory);

            var newCommand = new NewCommand();
            newCommand.WorkingDirectory = RestoredTestProjectDirectory;
            newCommand.Execute().Should().Pass();

            var restoreCommand = new RestoreCommand();
            restoreCommand.WorkingDirectory = RestoredTestProjectDirectory;
            restoreCommand.Execute().Should().Pass();
        }
    }
}
