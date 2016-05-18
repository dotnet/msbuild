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

            var testDestination = GetTestDestinationDirectoryPath(testProjectName, callingMethod, identifier);
            var testInstance = new TestInstance(testProjectDir, testDestination);
            return testInstance;
        }

        public TestDirectory CreateTestDirectory([CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var testDestination = GetTestDestinationDirectoryPath(string.Empty, callingMethod, identifier);

            return new TestDirectory(testDestination);
        }

        private string GetTestDestinationDirectoryPath(string testProjectName, string callingMethod, string identifier)
        {
// HACK: Make the TestAssetsManager create shorter paths for test assets.
// This is a workaround for https://github.com/dotnet/coreclr/issues/5046
// Remove this hack once the above issue is fixed.
/*#if NET451
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string baseDirectory = AppContext.BaseDirectory;
#endif*/

            string path = Path.Combine(GetRepoRoot(), "bin", callingMethod + identifier, testProjectName);
            Console.WriteLine($"[TestAssetsManager] The working directory for {testProjectName} is '{path}'");
            return path;
        }

        private static string GetRepoRoot()
        {

            if (!string.IsNullOrEmpty(s_repoRoot))
            {
                return s_repoRoot;
            }

#if NET451
            string directory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string directory = AppContext.BaseDirectory;
#endif

            while (!Directory.Exists(Path.Combine(directory, ".git")) && directory != null)
            {
                directory = Directory.GetParent(directory).FullName;
            }

            if (directory == null)
            {
                throw new Exception("Cannot find the git repository root");
            }

            s_repoRoot = directory;
            return s_repoRoot;
        }

        private static string s_repoRoot;
    }
}
