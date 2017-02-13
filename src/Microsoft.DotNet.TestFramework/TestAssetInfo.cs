// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.TestFramework
{
    public class TestAssetInfo
    {
        private readonly string [] FilesToExclude = { ".DS_Store", ".noautobuild" };

        public string AssetName { get; private set; }

        public FileInfo DotnetExeFile { get; private set; }

        public string ProjectFilePattern { get; private set; }

        public DirectoryInfo Root { get; private set; }

        internal TestAssetInfo(DirectoryInfo root, string assetName, FileInfo dotnetExeFile, string projectFilePattern)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                throw new ArgumentException("Argument cannot be null or whitespace", nameof(assetName));
            }

            if (dotnetExeFile == null)
            {
                throw new ArgumentNullException(nameof(dotnetExeFile));
            }

            if (string.IsNullOrWhiteSpace(projectFilePattern))
            {
                throw new ArgumentException("Argument cannot be null or whitespace", nameof(projectFilePattern));
            }

            Root = root;

            AssetName = assetName;

            DotnetExeFile = dotnetExeFile;

            ProjectFilePattern = projectFilePattern;
        }

        public TestAssetInstance CreateInstance([CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var instancePath = GetTestDestinationDirectory(callingMethod, identifier);

            var testInstance = new TestAssetInstance(this, instancePath);

            return testInstance;
        }

        internal IEnumerable<FileInfo> GetSourceFiles()
        {
            ThrowIfTestAssetDoesNotExist();

            return Root.GetFiles("*.*", SearchOption.AllDirectories)
                        .Where(f => !FilesToExclude.Contains(f.Name));
        }

        private DirectoryInfo GetTestDestinationDirectory(string callingMethod, string identifier)
        {
#if NET451
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string baseDirectory = AppContext.BaseDirectory;
#endif
            return new DirectoryInfo(Path.Combine(baseDirectory, callingMethod + identifier, AssetName));
        }

        private void ThrowIfTestAssetDoesNotExist()
        {
            if (!Root.Exists)
            { 
                throw new DirectoryNotFoundException($"Directory not found at '{Root.FullName}'"); 
            } 
        }
    }
}
