// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.TestFramework
{
    public class TestScenario
    {
        private static IDictionary<string, TestScenario> _scenarioCache = new Dictionary<string, TestScenario>();

        private TestScenario(string testSourceRoot, bool skipRestore, bool skipBuild)
        {
            SourceRoot = testSourceRoot;
            Projects = GetAllProjects(SourceRoot);

            if (!skipRestore)
            {
                Restore();
            }

            if (!skipBuild)
            {
                Build();
            }
        }

        private IEnumerable<string> Projects
        {
            get; set;
        }

        public string SourceRoot
        {
            get;
            private set;
        }

        public static TestScenario Create(string testSourceRoot, bool skipRestore = false, bool skipBuild = false)
        {
            TestScenario testScenario;
            lock (_scenarioCache)
            {
                if (!_scenarioCache.TryGetValue(testSourceRoot, out testScenario))
                {
                    testScenario = new TestScenario(testSourceRoot, skipRestore, skipBuild);
                    _scenarioCache.Add(testSourceRoot, testScenario);
                }
            }

            return testScenario;
        }

        public TestInstance CreateTestInstance([CallerMemberName] string callingMethod = "", string identifier = "")
        {
            string projectName = new DirectoryInfo(SourceRoot).Name;
            string testDestination = Path.Combine(AppContext.BaseDirectory, callingMethod + identifier, projectName);
            var testInstance = new TestInstance(this, testDestination);
            return testInstance;
        }

        internal void Build()
        {
            foreach (var project in Projects)
            {
                string[] buildArgs = new string[] { "build", project };
                var commandResult = Command.Create("dotnet", buildArgs)
                                        .CaptureStdOut()
                                        .CaptureStdErr()
                                        .Execute();

                Console.WriteLine(commandResult.StdOut);
                Console.WriteLine(commandResult.StdErr);
                int exitCode = commandResult.ExitCode;

                if (exitCode != 0)
                {

                    string message = string.Format("Command Failed - 'dotnet {0}' with exit code - {1}", string.Join(" ", buildArgs), exitCode);
                    throw new Exception(message);
                }
            }
        }

        private static IEnumerable<string> GetAllProjects(string sourceRoot)
        {
            return Directory.GetFiles(sourceRoot, "project.json", SearchOption.AllDirectories);
        }

        internal void Restore()
        {
            string[] restoreArgs = new string[] { "restore", SourceRoot };
            var commandResult = Command.Create("dotnet", restoreArgs)
                                        .CaptureStdOut()
                                        .CaptureStdErr()
                                        .Execute();

            Console.WriteLine(commandResult.StdOut);
            Console.WriteLine(commandResult.StdErr);
            int exitCode = commandResult.ExitCode;

            if (exitCode != 0)
            {
                string message = string.Format("Command Failed - 'dotnet {0}' with exit code - {1}", string.Join(" ", restoreArgs), exitCode);
                throw new Exception(message);
            }
        }
    }
}
