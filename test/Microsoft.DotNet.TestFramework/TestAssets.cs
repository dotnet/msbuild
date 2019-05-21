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
    public class TestAssets
    {
        private DirectoryInfo _root;

        private FileInfo _dotnetCsprojExe;

        private string _testWorkingFolder;

        public FileInfo DotnetCsprojExe => _dotnetCsprojExe;

        public TestAssets(DirectoryInfo assetsRoot, FileInfo dotnetCsprojExe, string testWorkingFolder)
        {
            if (assetsRoot == null)
            {
                throw new ArgumentNullException(nameof(assetsRoot));
            }

            if (dotnetCsprojExe == null)
            {
                throw new ArgumentNullException(nameof(dotnetCsprojExe));
            }

            if (!assetsRoot.Exists)
            {
                throw new DirectoryNotFoundException($"Directory not found at '{assetsRoot}'");
            }

            if (!dotnetCsprojExe.Exists)
            {
                throw new FileNotFoundException("Csproj dotnet executable must exist", dotnetCsprojExe.FullName);
            }

            _root = assetsRoot;

            _dotnetCsprojExe = dotnetCsprojExe;
            _testWorkingFolder = testWorkingFolder;
        }

        public TestAssetInfo Get(string name)
        {
            return Get(TestAssetKinds.TestProjects, name);
        }

        public TestAssetInfo Get(string kind, string name)
        {
            var assetDirectory = new DirectoryInfo(Path.Combine(_root.FullName, kind, name));

            return new TestAssetInfo(
                assetDirectory, 
                name, 
                this);
        }

        public DirectoryInfo CreateTestDirectory(string testProjectName = "temp", [CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var testDestination = GetTestDestinationDirectoryPath(testProjectName, callingMethod, identifier);

            var testDirectory = new DirectoryInfo(testDestination);

            testDirectory.EnsureExistsAndEmpty();

            return testDirectory;
        }

        private string GetTestDestinationDirectoryPath(string testProjectName, string callingMethod, string identifier)
        {
            string baseDirectory = AppContext.BaseDirectory;

            //  Find the name of the assembly the test comes from based on the the base directory and how the output path has been constructed
            string testAssemblyName = new DirectoryInfo(baseDirectory).Parent.Parent.Name;

            return Path.Combine(_testWorkingFolder, testAssemblyName, callingMethod + identifier, testProjectName);
        }
    }
}
