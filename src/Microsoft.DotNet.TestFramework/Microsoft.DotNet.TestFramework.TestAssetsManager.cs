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
    public class TestAssetsManager
    {
        public string AssetsRoot
        {
            get; private set;
        }

        public TestAssetsManager(string assetsRoot, bool doRestore = false, bool doBuild = false)
        {
            if (!Directory.Exists(assetsRoot))
            {
                throw new DirectoryNotFoundException($"Directory not found at '{assetsRoot}'");
            }

            AssetsRoot = assetsRoot;

            if (doRestore)
            {
                Restore();
            }

            if (doBuild)
            {
                Build();
            }
        }

        private void Restore()
        {
            string[] restoreArgs = new string[] { "restore", AssetsRoot };

            Console.WriteLine("Executing - 'dotnet {0}'", string.Join(" ", restoreArgs));
            var commandResult = Command.Create("dotnet", restoreArgs)
                                       .CaptureStdOut()
                                       .CaptureStdErr()
                                       .Execute();
            
            int exitCode = commandResult.ExitCode;

            if (exitCode != 0)
            {
                Console.WriteLine(commandResult.StdOut);
                Console.WriteLine(commandResult.StdErr);
                string message = string.Format("Command Failed - 'dotnet {0}' with exit code - {1}", string.Join(" ", restoreArgs), exitCode);
                throw new Exception(message);
            }
        }

        private void Build()
        {
            var projects = Directory.GetFiles(AssetsRoot, "project.json", SearchOption.AllDirectories);
            foreach (var project in projects)
            {
                string[] buildArgs = new string[] { "build", project };

                Console.WriteLine("Executing - 'dotnet {0}'", string.Join(" ", buildArgs));
                var commandResult = Command.Create("dotnet", buildArgs)
                                           .CaptureStdOut()
                                           .CaptureStdErr()
                                           .Execute();

                int exitCode = commandResult.ExitCode;

                if (exitCode != 0)
                {
                    Console.WriteLine(commandResult.StdOut);
                    Console.WriteLine(commandResult.StdErr);
                    string message = string.Format("Command Failed - 'dotnet {0}' with exit code - {1}", string.Join(" ", buildArgs), exitCode);
                    throw new Exception(message);
                }
            }
        }

        public TestInstance CreateTestInstance(string testProjectName, [CallerMemberName] string callingMethod = "", string identifier = "")
        {
            string testProjectDir = Path.Combine(AssetsRoot, testProjectName);

            if (!Directory.Exists(testProjectDir))
            {
                throw new Exception($"Cannot find '{testProjectName}' at '{AssetsRoot}'");
            }

            string testDestination = Path.Combine(AppContext.BaseDirectory, callingMethod + identifier, testProjectName);
            var testInstance = new TestInstance(testProjectDir, testDestination);
            return testInstance;
        }
    }
}
