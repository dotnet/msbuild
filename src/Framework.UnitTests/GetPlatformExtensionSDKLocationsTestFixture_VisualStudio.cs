// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public partial class GetPlatformExtensionSDKLocationsTestFixture
{
    /// <summary>
    /// Verify that the list of platforms is empty if we ask for an sdk that is not installed.
    /// </summary>
    [UnixOnlyFact]
    public void VerifyGetFoldersInVSInstalls_Unix()
        => ToolLocationHelper.GetFoldersInVSInstalls(null, null, "relativePath").Count().ShouldBe(0);

    [WindowsOnlyFact]
    public void VerifyFindRootFolderWhereAllFilesExist()
    {
        // create directories and files in them

        // root1
        //     subdir
        //         file1.txt
        //     file1.txt
        //  root2
        //     subdir
        //         file2.txt
        //     file1.txt
        string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyFindRootFolderWhereAllFilesExist");
        string[] rootDirectories = [Path.Combine(testDirectoryRoot, "Root1"), Path.Combine(testDirectoryRoot, "Root2")];

        for (int i = 0; i < rootDirectories.Length; i++)
        {
            // create directory
            string subdir = Path.Combine(rootDirectories[i], "Subdir");
            Directory.CreateDirectory(subdir);
            var fileInSubDir = string.Format("file{0}.txt", i + 1);
            File.Create(Path.Combine(rootDirectories[i], "file1.txt")).Close();
            File.Create(Path.Combine(subdir, fileInSubDir)).Close();
        }

        string roots = string.Join(";", rootDirectories);

        ToolLocationHelper.FindRootFolderWhereAllFilesExist(roots, "file1.txt").ShouldBe(rootDirectories[0]);
        ToolLocationHelper.FindRootFolderWhereAllFilesExist(roots, @"file1.txt;subdir\file2.txt").ShouldBe(rootDirectories[1]);
        ToolLocationHelper.FindRootFolderWhereAllFilesExist(roots, @"file1.txt;subdir\file3.txt").ShouldBe(String.Empty);
        ToolLocationHelper.FindRootFolderWhereAllFilesExist(@"c:<>;" + roots, "file1.txt").ShouldBe(rootDirectories[0]); // should ignore invalid dir
    }
}
