// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class RepoDirectoriesProvider
    {
        public readonly static string RepoRoot;

        public readonly static string TestWorkingFolder;
        public readonly static string DotnetUnderTest;
        public readonly static string DotnetRidUnderTest;

        static RepoDirectoriesProvider()
        {

#if NET451
            string directory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string directory = AppContext.BaseDirectory;
#endif

            while (directory != null)
            {
                var gitDirOrFile = Path.Combine(directory, ".git");
                if (Directory.Exists(gitDirOrFile) || File.Exists(gitDirOrFile))
                {
                    break;
                }
                directory = Directory.GetParent(directory)?.FullName;
            }

            RepoRoot = directory;

            TestWorkingFolder = Environment.GetEnvironmentVariable("CORESDK_TEST_FOLDER");
            if (string.IsNullOrEmpty(TestWorkingFolder))
            {
                TestWorkingFolder = Path.Combine(AppContext.BaseDirectory, "Tests");
            }

            DotnetUnderTest = Environment.GetEnvironmentVariable("DOTNET_UNDER_TEST");
            string dotnetExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            if (string.IsNullOrEmpty(DotnetUnderTest))
            {
                if (RepoRoot == null)
                {
                    DotnetUnderTest = "dotnet" + dotnetExtension;
                }
                else
                {
                    string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
                    DotnetUnderTest = Path.Combine(RepoRoot, "artifacts", "bin", "redist", configuration, "dotnet", "dotnet" + dotnetExtension);
                }
            }

            //  TODO: Resolve dotnet folder even if DotnetUnderTest doesn't have full path
            var sdkFolders = Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(DotnetUnderTest), "sdk"));
            sdkFolders.Length.Should().Be(1, "Only one SDK folder is expected in the layout");

            var sdkFolder = sdkFolders.Single();
            var versionFile = Path.Combine(sdkFolder, ".version");

            var lines = File.ReadAllLines(versionFile);
            DotnetRidUnderTest = lines[2].Trim();
        }

    }
}
