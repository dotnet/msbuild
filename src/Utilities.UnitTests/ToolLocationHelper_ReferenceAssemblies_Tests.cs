// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using SharedDotNetFrameworkArchitecture = Microsoft.Build.Shared.DotNetFrameworkArchitecture;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    [Fact]
    public void GenerateReferencAssemblyPathAllElements()
    {
        string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                                             ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                                             : "/usr/lib";
        string targetFrameworkIdentifier = "Compact Framework";
        Version targetFrameworkVersion = new Version("1.0");
        string targetFrameworkProfile = "PocketPC";

        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

        string expectedPath = Path.Combine(targetFrameworkRootPath, targetFrameworkIdentifier);
        expectedPath = Path.Combine(expectedPath, "v" + targetFrameworkVersion);
        expectedPath = Path.Combine(expectedPath, "Profile");
        expectedPath = Path.Combine(expectedPath, targetFrameworkProfile);

        string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        path.ShouldBe(expectedPath, StringCompareShould.IgnoreCase);
    }

    [Fact]
    public void GenerateReferencAssemblyPathNoProfile()
    {
        string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                                             ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                                             : "/usr/lib";
        string targetFrameworkIdentifier = "Compact Framework";
        Version targetFrameworkVersion = new Version("1.0");
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, string.Empty);
        string expectedPath = Path.Combine(targetFrameworkRootPath, targetFrameworkIdentifier);
        expectedPath = Path.Combine(expectedPath, "v" + targetFrameworkVersion);

        string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        path.ShouldBe(expectedPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Make sure if the profile has invalid chars which would be used as part of path generation that we get an InvalidOperationException
    /// which indicates there was a problem generating the reference assembly path.
    /// </summary>
    [Fact]
    public void GenerateReferencAssemblyInvalidProfile()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                                                ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                                                : "/usr/lib";
            string targetFrameworkIdentifier = "Compact Framework";
            Version targetFrameworkVersion = new Version("1.0");
            string targetFrameworkProfile = "PocketPC" + new string(Path.GetInvalidFileNameChars());

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

            FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        });

    /// <summary>
    /// Make sure if the identifier has invalid chars which would be used as part of path generation that we get an InvalidOperationException
    /// which indicates there was a problem generating the reference assembly path.
    /// </summary>
    [Fact]
    public void GenerateReferencAssemblyInvalidIdentifier()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                                                    ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                                                    : "/usr/lib";
            string targetFrameworkIdentifier = "Compact Framework" + new string(Path.GetInvalidFileNameChars());
            Version targetFrameworkVersion = new Version("1.0");
            string targetFrameworkProfile = "PocketPC";

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

            FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        });

    /// <summary>
    /// Make sure if the moniker and the root make a too long path that an InvalidOperationException is raised
    /// which indicates there was a problem generating the reference assembly path.
    /// </summary>
    [WindowsFullFrameworkOnlyFact]
    public void GenerateReferencAssemblyPathTooLong()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string pathTooLong = new string('a', 500);

            string targetFrameworkRootPath = NativeMethodsShared.IsWindows
                                                ? @"c:\Program Files\Reference Assemblies\Microsoft\Framework"
                                                : "/usr/lib";
            string targetFrameworkIdentifier = "Compact Framework" + pathTooLong;
            Version targetFrameworkVersion = new Version("1.0");
            string targetFrameworkProfile = "PocketPC";

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

            FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
        });

    /// <summary>
    /// Verify the chaining method returns a null if there is no redist list file for the framework we are trying to chain with. This is ok because the lack of a redist list file means we
    /// do not have anything to chain with.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsNoRedistList()
    {
        string path = ToolLocationHelper.ChainReferenceAssemblyPath(@"PathDoesNotExistSoICannotChain");
        path.ShouldBeNull(); // " Expected the path to be null when the path to the FrameworkList.xml does not exist"
    }

    /// <summary>
    /// Verify we do not hang, crash, go on forever if there is a circular reference with the include frameworks. What should happen is
    /// we should notice that we have already chained to a given framework and not try and chain with it again.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsCircularRefernce()
    {
        string redistString41 = "<FileList Redist='Random' IncludeFramework='v4.0'>" +
                                 "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                              "</FileList >";

        string redistString40 = "<FileList Redist='Random'>" +
                                   "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                "</FileList >";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsChain");

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
        string redist40Directory = Path.Combine(tempDirectory, "v4.0", "RedistList") + Path.DirectorySeparatorChar;
        string redist40 = Path.Combine(redist40Directory, "FrameworkList.xml");
        try
        {
            Directory.CreateDirectory(redist41Directory);
            Directory.CreateDirectory(redist40Directory);
            File.WriteAllText(redist40, redistString40);
            File.WriteAllText(redist41, redistString41);

            string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));

            string expectedChainedPath = Path.Combine(tempDirectory, "v4.0");
            expectedChainedPath.ShouldBe(path, StringCompareShould.IgnoreCase);
        }
        finally
        {
            if (Directory.Exists(redist40Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redist40Directory, true);
            }

            if (Directory.Exists(redist41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redist41Directory, true);
            }
        }
    }

    /// <summary>
    /// Verify the case where there is no Included framework attribute, there should be no errors and we should continue on as if there were no further framework chained with the current one
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsNoInclude()
    {
        string redistString41 = "<FileList Redist='Random'>" +
                                    "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                 "</FileList >";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsNoInclude");

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
        try
        {
            Directory.CreateDirectory(redist41Directory);
            File.WriteAllText(redist41, redistString41);
            string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));
            path.ShouldBe(string.Empty); // "Expected the path to be empty"
        }
        finally
        {
            if (Directory.Exists(redist41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redist41Directory, true);
            }
        }
    }

    /// <summary>
    /// Verify the case where the include framework is empty, this is ok, we should error but should just continue on as if there was no chaining of the redist list file.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsEmptyInclude()
    {
        string redistString41 = "<FileList Redist='Random' IncludeFramework=''>" +
                                    "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                 "</FileList >";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsNoInclude");

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
        try
        {
            Directory.CreateDirectory(redist41Directory);
            File.WriteAllText(redist41, redistString41);
            string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));
            path.ShouldBe(string.Empty); // "Expected the path to be empty"
        }
        finally
        {
            if (Directory.Exists(redist41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redist41Directory, true);
            }
        }
    }

    /// <summary>
    /// Verify the case where the redist is a valid xml file but does not have the FileListElement, this is to make sure we do not crash or get an exception if the FileList element cannot be found
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsNoFileList()
    {
        string redistString41 = "<FileListNOT Redist='Random'>" +
                                    "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                 "</FileListNOT >";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsNoFileList");

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
        try
        {
            Directory.CreateDirectory(redist41Directory);
            File.WriteAllText(redist41, redistString41);
            string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));
            path.ShouldBe(string.Empty); // "Expected the path to be empty"
        }
        finally
        {
            if (Directory.Exists(redist41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redist41Directory, true);
            }
        }
    }

    /// <summary>
    /// Make sure we get the correct exception when there is no xml in the redist list file
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistExistsBadFile()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string redistString40 = "GARBAGE";
            string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsBadFile");

            string redist40Directory = Path.Combine(tempDirectory, "v4.0", "RedistList") + Path.DirectorySeparatorChar;
            string redist40 = Path.Combine(redist40Directory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(redist40Directory);
                File.WriteAllText(redist40, redistString40);

                string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.0"));
                path.ShouldBeNull(); // "Expected the path to be null"
            }
            finally
            {
                if (Directory.Exists(redist40Directory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(redist40Directory, true);
                }
            }
        });

    /// <summary>
    /// Make sure we get the correct exception when the xml file points to an included framework which does not exist.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistPointsToInvalidInclude()
    {
        string redistString41 = "<FileList Redist='Random' IncludeFramework='IDontExist'>" +
                                          "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                       "</FileList>";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistPointsToInvalidInclude");

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
        string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
        try
        {
            Directory.CreateDirectory(redist41Directory);
            File.WriteAllText(redist41, redistString41);

            string path = ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
            path.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(redist41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redist41Directory, true);
            }
        }
    }

    /// <summary>
    /// Make sure we get the correct exception when the xml file points to an included framework which has invalid path chars.
    /// </summary>
    [Fact]
    public void ChainReferenceAssembliesRedistInvalidPathChars()
        => Should.Throw<InvalidOperationException>(() =>
    {
        char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

        string redistString41 = "<FileList Redist='Random' IncludeFramework='" + new string(invalidFileNameChars) + "'>" +
                                        "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                    "</FileList>";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistInvalidPathChars");

        string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
        string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
        string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
        try
        {
            Directory.CreateDirectory(redist41Directory);
            File.WriteAllText(redist41, redistString41);

            ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
        }
        finally
        {
            if (Directory.Exists(redist41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redist41Directory, true);
            }
        }
    });

    /// <summary>
    /// Make sure we get the correct exception when the xml file points to an included framework which has invalid path chars.
    /// </summary>
    [WindowsFullFrameworkOnlyFact]
    public void ChainReferenceAssembliesRedistPathTooLong()
        => Should.Throw<InvalidOperationException>(() =>
        {
            string tooLong = new string('a', 500);
            string redistString41 = "<FileList Redist='Random' IncludeFramework='" + tooLong + "'>" +
                                                "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                            "</FileList>";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistPathTooLong");

            string redist41Directory = Path.Combine(tempDirectory, "v4.1", "RedistList") + Path.DirectorySeparatorChar;
            string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
            string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
            try
            {
                Directory.CreateDirectory(redist41Directory);
                File.WriteAllText(redist41, redistString41);

                ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
            }
            finally
            {
                if (Directory.Exists(redist41Directory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(redist41Directory, true);
                }
            }
        });

    /// <summary>
    /// Verify the case where we are chaining redist lists and they are properly formatted
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithRootGoodWithChain()
    {
        string redistString41 = "<FileList Redist='Random' IncludeFramework='v4.0'>" +
                                 "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                              "</FileList >";

        string redistString40 = "<FileList Redist='Random' IncludeFramework='v3.9'>" +
                                   "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                "</FileList >";

        string redistString39 = "<FileList Redist='Random'>" +
                                     "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                  "</FileList >";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "GetPathToReferenceAssembliesWithRootGoodWithChain");

        string framework41Directory = Path.Combine(tempDirectory, "MyFramework", "v4.1") + Path.DirectorySeparatorChar;
        string framework41redistDirectory = Path.Combine(framework41Directory, "RedistList");
        string framework41RedistList = Path.Combine(framework41redistDirectory, "FrameworkList.xml");

        string framework40Directory = Path.Combine(tempDirectory, "MyFramework", "v4.0") + Path.DirectorySeparatorChar;
        string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");
        string framework40RedistList = Path.Combine(framework40redistDirectory, "FrameworkList.xml");

        string framework39Directory = Path.Combine(tempDirectory, "MyFramework", "v3.9") + Path.DirectorySeparatorChar;
        string framework39redistDirectory = Path.Combine(framework39Directory, "RedistList");
        string framework39RedistList = Path.Combine(framework39redistDirectory, "FrameworkList.xml");

        try
        {
            Directory.CreateDirectory(framework41redistDirectory);
            Directory.CreateDirectory(framework40redistDirectory);
            Directory.CreateDirectory(framework39redistDirectory);

            File.WriteAllText(framework39RedistList, redistString39);
            File.WriteAllText(framework40RedistList, redistString40);
            File.WriteAllText(framework41RedistList, redistString41);

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("MyFramework", new Version("4.1"));
            IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(tempDirectory, frameworkName);

            directories.Count.ShouldBe(3); // "Expected the method to return three paths."
            directories[0].ShouldBe(framework41Directory, StringCompareShould.IgnoreCase);
            directories[1].ShouldBe(framework40Directory, StringCompareShould.IgnoreCase);
            directories[2].ShouldBe(framework39Directory, StringCompareShould.IgnoreCase);
        }
        finally
        {
            if (Directory.Exists(framework41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(framework41Directory, true);
            }

            if (Directory.Exists(framework40Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(framework40Directory, true);
            }

            if (Directory.Exists(framework39Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(framework39Directory, true);
            }
        }
    }

    /// <summary>
    /// Verify the correct display name returned
    /// </summary>
    [Fact]
    public void DisplayNameGeneration()
    {
        string redistString40 = "<FileList Redist='Random' Name='MyFramework 4.0' >" +
                                   "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                "</FileList >";

        string redistString39 = "<FileList Redist='Random'>" +
                                     "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                  "</FileList >";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "DisplayNameGeneration");

        string framework40Directory = Path.Combine(tempDirectory, "MyFramework", "v4.0")
                                      + Path.DirectorySeparatorChar;
        string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");
        string framework40RedistList = Path.Combine(framework40redistDirectory, "FrameworkList.xml");

        string framework39Directory =
            Path.Combine(tempDirectory, "MyFramework", "v3.9", "Profile", "Client");
        string framework39redistDirectory = Path.Combine(framework39Directory, "RedistList");
        string framework39RedistList = Path.Combine(framework39redistDirectory, "FrameworkList.xml");

        try
        {
            Directory.CreateDirectory(framework40redistDirectory);
            Directory.CreateDirectory(framework39redistDirectory);

            File.WriteAllText(framework39RedistList, redistString39);
            File.WriteAllText(framework40RedistList, redistString40);

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("MyFramework", new Version("4.0"));
            string displayName40 = ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(framework40Directory, frameworkName);

            frameworkName = new FrameworkNameVersioning("MyFramework", new Version("3.9"), "Client");
            string displayName39 = ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(framework39Directory, frameworkName);
            displayName40.ShouldBe("MyFramework 4.0", StringCompareShould.IgnoreCase);
            displayName39.ShouldBe("MyFramework v3.9 Client", StringCompareShould.IgnoreCase);
        }
        finally
        {
            if (Directory.Exists(framework40Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(framework40Directory, true);
            }

            if (Directory.Exists(framework39Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(framework39Directory, true);
            }
        }
    }

    /// <summary>
    /// Make sure we do not crash if there is a circular reference in the redist lists, we should only have a path in our reference assembly list once.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesWithRootCircularReference()
    {
        string redistString41 = "<FileList Redist='Random' IncludeFramework='v4.0'>" +
                                 "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                              "</FileList >";

        string redistString40 = "<FileList Redist='Random' IncludeFramework='v4.1'>" +
                                   "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                "</FileList >";

        string tempDirectory = Path.Combine(Path.GetTempPath(), "GetPathToReferenceAssembliesWithRootCircularReference");

        string framework41Directory = Path.Combine(tempDirectory, "MyFramework", "v4.1")
                                      + Path.DirectorySeparatorChar;
        string framework41redistDirectory = Path.Combine(framework41Directory, "RedistList");
        string framework41RedistList = Path.Combine(framework41redistDirectory, "FrameworkList.xml");

        string framework40Directory = Path.Combine(tempDirectory, "MyFramework", "v4.0")
                                      + Path.DirectorySeparatorChar;
        string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");
        string framework40RedistList = Path.Combine(framework40redistDirectory, "FrameworkList.xml");

        try
        {
            Directory.CreateDirectory(framework41redistDirectory);
            Directory.CreateDirectory(framework40redistDirectory);

            File.WriteAllText(framework40RedistList, redistString40);
            File.WriteAllText(framework41RedistList, redistString41);

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("MyFramework", new Version("4.1"));
            IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(tempDirectory, frameworkName);

            directories.Count.ShouldBe(2); // "Expected the method to return two paths."
            directories[0].ShouldBe(framework41Directory, StringCompareShould.IgnoreCase);
            directories[1].ShouldBe(framework40Directory, StringCompareShould.IgnoreCase);
        }
        finally
        {
            if (Directory.Exists(framework41Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(framework41Directory, true);
            }

            if (Directory.Exists(framework40Directory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(framework40Directory, true);
            }
        }
    }

    /// <summary>
    /// Test the case where the root path is a string but the framework name is null.
    /// We should expect the correct argument null exception
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullFrameworkName()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies("Not Null String", (FrameworkNameVersioning)null));

    /// <summary>
    /// Make sure we get the correct exception when both parameters are null
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentNameandFrameworkName()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(null, (FrameworkNameVersioning)null));

    /// <summary>
    /// Make sure we get the correct exception when the root is null but the frameworkname is not null
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameNullRoot()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(null, new FrameworkNameVersioning("Ident", new Version("2.0"))));

    /// <summary>
    /// Make sure we get the correct exception when the root is null but the frameworkname is not null
    /// With no framework name we cannot generate the path
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameEmptyRoot()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(string.Empty, new FrameworkNameVersioning("Ident", new Version("2.0"))));

    /// <summary>
    /// Make sure we get the correct exception when the root is null but the frameworkname is not empty to make sure we cover the different input cases
    /// With no root we cannot properly generate the path.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameEmptyRoot2()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies(string.Empty, new FrameworkNameVersioning("Ident", new Version("2.0"))));

    /// <summary>
    /// Test the case where the method which only takes in a FrameworkName will throw an exception when
    /// the input is null since a null framework name is not useful
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesDefaultLocationNullFrameworkName()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPathToReferenceAssemblies((FrameworkNameVersioning)null));

    /// <summary>
    /// Verify the method correctly returns the 4.8 reference assembly location information if .net 4.8 and
    /// its corresponding reference assemblies are installed.
    /// .NET 4.8 should always be installed for our tests. We cannot validly verify on Windows that by adding a check
    /// that ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version48)
    /// returns something reasonable because later versions of the framework overwrote the current version in
    /// place, which means it just looks for a folder starting with v4.0 in the right spot for any higher version.
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesDefaultLocation48()
    {
        if (ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version48) != null)
        {
            FrameworkNameVersioning frameworkName = new(".NETFramework", new Version("4.8"));
            IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName);
            directories.Count.ShouldBe(1); // "Expected the method to return one path."

            string referenceAssemblyPath = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version48);
            directories[0].ShouldBe(referenceAssemblyPath, StringCompareShould.IgnoreCase);
        }

        // else
        // "Ignored because v4.8 did not seem to be installed"
    }

    /// <summary>
    /// Test the case where the framework requested does not exist. Since we do an existence check before returning the path this non existent path should return an empty list
    /// </summary>
    [Fact]
    public void GetPathToReferenceAssembliesDefaultLocation99()
    {
        string targetFrameworkIdentifier = ".Net Framework";
        Version targetFrameworkVersion = new Version("99.99");

        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, string.Empty);

        IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName);
        directories.Count.ShouldBe(0); // "Expected the method to return no paths."
    }

    /// <summary>
    /// Make sure we choose the correct path for program files based on the operating system
    /// </summary>
    [WindowsOnlyFact]
    public void TestGenerateProgramFiles32()
    {
        Environment.SpecialFolder folder = Environment.Is64BitOperatingSystem ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles;
        string programFilesX86 = Environment.GetFolderPath(folder);
        string result = FrameworkLocationHelper.GenerateProgramFiles32();

        // 32-bit OS: "Expected to use program files"
        // 64-bit OS: "Expected to use program files x86"
        result.ShouldBe(programFilesX86, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Verify we get the correct reference assembly path out of the framework location helper
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "No ProgramFiles known location outside Windows.")]
    public void TestGeneratedReferenceAssemblyPath()
    {
        string programFiles32 = FrameworkLocationHelper.GenerateProgramFiles32();
        string referenceAssemblyRoot = FrameworkLocationHelper.GenerateProgramFilesReferenceAssemblyRoot();
        string pathToCombineWith = "Reference Assemblies\\Microsoft\\Framework";
        string combinedPath = Path.Combine(programFiles32, pathToCombineWith);
        string fullPath = Path.GetFullPath(combinedPath);

        referenceAssemblyRoot.ShouldBe(fullPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Verify when 20 is simulated to be installed that the method returns the 2.0 directory
    /// </summary>
    [Fact]
    public void LegacyFramework20Good()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("2.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNet20Installed = true
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(1);
        list[0].ShouldBe(LegacyFrameworkTestHelper.DotNet20FrameworkPath);
    }

    /// <summary>
    /// Verify when 20 is simulated to not be installed that the method returns an empty list
    /// </summary>
    [Fact]
    public void LegacyFramework20NotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("2.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// Verify when 30 is simulated to be installed that the method returns the 3.0 directory
    /// </summary>
    [Fact]
    public void LegacyFramework30Good()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNetReferenceAssemblies30Installed = true,
            DotNet30Installed = true,
            DotNet20Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(3);
        list[0].ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath);
        list[1].ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath);
        list[2].ShouldBe(LegacyFrameworkTestHelper.DotNet20FrameworkPath);
    }

    /// <summary>
    /// Verify when 30 is simulated to not be installed that the method returns an empty list
    /// </summary>
    [Fact]
    public void LegacyFramework30NotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNetReferenceAssemblies30Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// Verify when the 30 reference assemblies are simulated to not be installed that the method returns an empty list
    /// </summary>
    [Fact]
    public void LegacyFramework30ReferenceAssembliesNotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNet30Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// Verify when 30 is installed but 2.0 is not installed that we only get one of the paths back.
    /// </summary>
    [Fact]
    public void LegacyFramework30WithNo20Installed()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNet30Installed = true,
            DotNetReferenceAssemblies30Installed = true,
        };

        // Note no 2.0 installed
        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(2);
        list[0].ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, StringCompareShould.IgnoreCase);
        list[1].ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Verify when 35 is simulated to be installed that the method returns the 3.5 directory
    /// </summary>
    [Fact]
    public void LegacyFramework35Good()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNetReferenceAssemblies35Installed = true,
            DotNetReferenceAssemblies30Installed = true,
            DotNet30Installed = true,
            DotNet35Installed = true,
            DotNet20Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(5);
        list[0].ShouldBe(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath);
        list[1].ShouldBe(LegacyFrameworkTestHelper.DotNet35FrameworkPath);
        list[2].ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath);
        list[3].ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath);
        list[4].ShouldBe(LegacyFrameworkTestHelper.DotNet20FrameworkPath);
    }

    /// <summary>
    /// Verify when 35 is simulated to not be installed that the method returns an empty list
    /// </summary>
    [Fact]
    public void LegacyFramework35NotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNetReferenceAssemblies35Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// Verify when 35 reference assembly are simulated to not be installed that the method returns an empty list
    /// </summary>
    [Fact]
    public void LegacyFramework35ReferenceAssembliesNotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNet35Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// Make sure when we are targeting .net framework 3.5 and are on a 64 bit machine we get the correct framework path.
    ///
    /// We are on a 64 bit machine
    /// Targeting .net framework 3.5
    ///
    /// 1) Target platform is x86. We expect to get the 32 bit framework directory
    /// 2) Target platform is x64, we expect to get the 64 bit framework directory
    /// 3) Target platform is Itanium, we expect to get the 64 bit framework directory
    /// 3) Target platform is some other value (AnyCpu, or anything else)  expect the framework directory for the "current" bitness of the process we are running under.
    ///
    /// </summary>
    [Fact]
    public void GetPathToStandardLibraries64Bit35()
    {
        string frameworkDirectory2032bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness32);
        string frameworkDirectory2064bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness64);
        string frameworkDirectory20Current = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Current);

        if (!Environment.Is64BitOperatingSystem)
        {
            // "Not 64 bit OS "
            return;
        }

        if (string.IsNullOrEmpty(frameworkDirectory2032bit) || string.IsNullOrEmpty(frameworkDirectory2064bit) || string.IsNullOrEmpty(frameworkDirectory20Current))
        {
            // ".Net 2.0 not installed: checked current {0} :: 64 bit :: {1} :: 32 bit {2}", frameworkDirectory20Current, frameworkDirectory2064bit, frameworkDirectory2032bit
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x86");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x64");
        pathToFramework.ShouldBe(frameworkDirectory2064bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "itanium");
        pathToFramework.ShouldBe(frameworkDirectory2064bit, StringCompareShould.IgnoreCase);

        if (!Environment.Is64BitProcess)
        {
            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "RandomPlatform");
            pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);
        }
        else
        {
            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "RandomPlatform");
            pathToFramework.ShouldBe(frameworkDirectory2064bit, StringCompareShould.IgnoreCase);
        }
    }

    /// <summary>
    /// Make sure when we are targeting .net framework 3.5 and are on a 64 bit machine we get the correct framework path.
    ///
    /// We are on a 64 bit machine
    /// Targeting .net framework 4.0
    ///
    /// We expect to always get the same path which is returned by GetPathToReferenceAssemblies.
    /// </summary>
    [Fact]
    public void GetPathToStandardLibraries64Bit40()
    {
        IList<string> referencePaths = ToolLocationHelper.GetPathToReferenceAssemblies(new FrameworkNameVersioning(".NETFramework", new Version("4.0")));

        if (!Environment.Is64BitOperatingSystem)
        {
            // "Not 64 bit OS "
            return;
        }

        if (referencePaths.Count == 0)
        {
            // ".Net 4.0 not installed"
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x86");
        string dotNet40Path = FileUtilities.EnsureNoTrailingSlash(referencePaths[0]);
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x64");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "itanium");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "RandomPlatform");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Make sure when we are targeting .net framework 3.5 and are on a 32 bit machine we get the correct framework path.
    ///
    /// We are on a 32 bit machine
    /// Targeting .net framework 3.5
    ///
    /// 1) Target platform is x86. We expect to get the 32 bit framework directory
    /// 2) Target platform is x64, we expect to get the 32 bit framework directory
    /// 3) Target platform is Itanium, we expect to get the 32 bit framework directory
    /// 3) Target platform is some other value (AnyCpu, or anything else)  expect the framework directory for the "current" bitness of the process we are running under. In the
    ///    case of the unit test this should be the 32 bit framework directory.
    ///
    /// </summary>
    [Fact]
    public void GetPathToStandardLibraries32Bit35()
    {
        string frameworkDirectory2032bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness32);
        string frameworkDirectory20Current = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Current);

        if (Environment.Is64BitOperatingSystem)
        {
            // "Is a 64 bit OS "
            return;
        }

        if (string.IsNullOrEmpty(frameworkDirectory2032bit) || string.IsNullOrEmpty(frameworkDirectory20Current))
        {
            // ".Net 2.0 not installed: checked current {0} :: 32 bit {2}", frameworkDirectory20Current, frameworkDirectory2032bit
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x86");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "x64");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "itanium");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", string.Empty, "RandomPlatform");
        pathToFramework.ShouldBe(frameworkDirectory2032bit, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Make sure when we are targeting .net framework 4.0 and are on a 32 bit machine we get the correct framework path.
    ///
    /// We are on a 32 bit machine
    /// Targeting .net framework 4.0
    ///
    /// We expect to always get the same path which is returned by GetPathToReferenceAssemblies.
    /// </summary>
    [Fact]
    public void GetPathToStandardLibraries32Bit40()
    {
        IList<string> referencePaths = ToolLocationHelper.GetPathToReferenceAssemblies(new FrameworkNameVersioning(".NETFramework", new Version("4.0")));

        if (Environment.Is64BitOperatingSystem)
        {
            // "Is 64 bit OS "
            return;
        }

        if (referencePaths.Count == 0)
        {
            // ".Net 4.0 not installed"
            return;
        }

        string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x86");
        string dotNet40Path = FileUtilities.EnsureNoTrailingSlash(referencePaths[0]);
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "x64");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "itanium");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);

        pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", string.Empty, "RandomPlatform");
        pathToFramework.ShouldBe(dotNet40Path, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Verify when 35 is installed but 2.0 is not installed we to find 3.5 and 3.0 but no 2.0 because it does not exist.
    /// </summary>
    [Fact]
    public void LegacyFramework35WithNo20Installed()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNetReferenceAssemblies35Installed = true,
            DotNetReferenceAssemblies30Installed = true,
            DotNet35Installed = true,
            DotNet30Installed = true,
        };

        // Note no 2.0 installed

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(4);
        list[0].ShouldBe(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath, StringCompareShould.IgnoreCase);
        list[1].ShouldBe(LegacyFrameworkTestHelper.DotNet35FrameworkPath, StringCompareShould.IgnoreCase);
        list[2].ShouldBe(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, StringCompareShould.IgnoreCase);
        list[3].ShouldBe(LegacyFrameworkTestHelper.DotNet30FrameworkPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Verify when 35 is installed but 3.0 is not installed we expect not to find 3.0 or 2.0.
    /// </summary>
    [Fact]
    public void LegacyFramework35WithNo30Installed()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNetReferenceAssemblies35Installed = true,
            DotNet35Installed = true,
            DotNet20Installed = true,
        };

        // Note no 3.0

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(2);
        list[0].ShouldBe(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath, StringCompareShould.IgnoreCase);
        list[1].ShouldBe(LegacyFrameworkTestHelper.DotNet35FrameworkPath, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Verify when 40 is simulated to not be installed that the method returns an empty list
    /// </summary>
    [Fact]
    public void LegacyFramework40NotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("4.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// Verify when 40 reference assemblies are installed but the dot net framework is not, in this case we return empty indicating .net 4.0 is not properly installed
    /// </summary>
    [Fact]
    public void LegacyFramework40DotNetFrameworkDirectoryNotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("4.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNetReferenceAssemblies40Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    /// <summary>
    /// Verify when 40 reference assemblies are installed but the dot net framework is not we only get one of the paths back, this is because right now the assemblies are not in the right location
    /// </summary>
    [Fact]
    public void LegacyFramework40DotNetReferenceAssemblyDirectoryNotInstalled()
    {
        FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("4.0"));
        LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper
        {
            DotNet40Installed = true,
        };

        IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
        list.Count.ShouldBe(0);
    }

    [Fact]
    public void GetPathToStandardLibrariesWithCustomTargetFrameworkRoot()
    {
        using (var env = TestEnvironment.Create())
        {
            string frameworkName = "Foo Framework";
            string frameworkVersion = "v0.1";
            string rootDir = Path.Combine(env.DefaultTestDirectory.Path, "framework-root");

            string asmPath = CreateNewFrameworkAndGetAssembliesPath(env, frameworkName, frameworkVersion, rootDir);

            string stdLibPath = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, rootDir);
            stdLibPath.ShouldBe(asmPath);
        }
    }

    [Fact]
    public void GetPathToStandardLibrariesWithNullTargetFrameworkRootPath()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        string v45Path = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty);
        // This look up should fall back the default path with the .NET frameworks
        string v45PathWithNullRoot = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null);

        v45PathWithNullRoot.ShouldBe(v45Path);
    }

    [Fact]
    public void GetPathToStandardLibrariesWithCustomTargetFrameworkInFallbackSearchPathAndNullRoot()
    {
        using (var env = TestEnvironment.Create())
        {
            string frameworkName = "Foo Framework";
            string frameworkVersion = "v0.1";
            string customFrameworkRootPath = Path.Combine(env.DefaultTestDirectory.Path, "framework-root");

            string asmPath = CreateNewFrameworkAndGetAssembliesPath(env, frameworkName, frameworkVersion, customFrameworkRootPath);
            string fallbackSearchPaths = $"/foo/bar;{customFrameworkRootPath};/a/b";

            string stdLibPath = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, null, fallbackSearchPaths);
            stdLibPath.ShouldBe(asmPath);
        }
    }

    [Fact]
    public void GetPathToStandardLibrariesWithCustomRootAndCustomTargetFrameworkInFallbackSearchPath()
    {
        // We are creating the same framework in the root path and in a second location, used as a
        // fallback search path. When trying to find the framework, we should always resolve to
        // the framework in the root path, because the search order is:
        //  1. rootPath or default path, if null
        //  2. fallback search paths
        using (var env = TestEnvironment.Create())
        {
            string frameworkName = "Foo Framework";
            string frameworkVersion = "v0.1";
            string rootDir = Path.Combine(env.CreateFolder().Path, "framework-root");
            string fallbackPath = Path.Combine(env.CreateFolder().Path, "framework-root");

            string asmPathForRoot = CreateNewFrameworkAndGetAssembliesPath(env, frameworkName, frameworkVersion, rootDir);
            CreateNewFrameworkAndGetAssembliesPath(env, frameworkName, frameworkVersion, fallbackPath);
            string fallbackSearchPaths = $"/foo/bar;{fallbackPath};/a/b";

            string stdLibPath = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, rootDir, fallbackSearchPaths);

            // the path should be for the framework in the root path
            stdLibPath.ShouldBe(asmPathForRoot);
        }
    }

    [Fact]
    public void GetPathToStandardLibrariesWithNullTargetFrameworkFallbackSearchPaths()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        string v45Path = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty);

        // This look up should fall back the default path with the .NET frameworks
        string v45PathWithNullRoot = ToolLocationHelper.GetPathToStandardLibraries(frameworkName, frameworkVersion, string.Empty, null, null);

        v45PathWithNullRoot.ShouldBe(v45Path);
    }

    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInRoot()
    {
        using (var env = TestEnvironment.Create())
        {
            string customFrameworkDirToUse = Path.Combine(env.CreateFolder().Path, "framework-root");

            CheckGetPathToReferenceAssemblies(
                env,
                customFrameworkDirToUse,
                null,
                (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string fallbackSearchPaths)
                    => ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, "v" + frameworkVersion, frameworkProfile, customFrameworkDir));
        }
    }

    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInFallbackPath()
    {
        using (var env = TestEnvironment.Create())
        {
            string customFrameworkDirToUse = Path.Combine(env.CreateFolder().Path, "framework-root");
            string searchPaths = $"/foo/bar;{customFrameworkDirToUse}";
            string rootDir = env.CreateFolder().Path;

            CheckGetPathToReferenceAssemblies(
                env,
                customFrameworkDirToUse,
                searchPaths,
                (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string fallbackSearchPaths)
                    => ToolLocationHelper.GetPathToReferenceAssemblies(
                        frameworkName, "v" + frameworkVersion, frameworkProfile, rootDir, fallbackSearchPaths));
        }
    }

    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInFallbackPathAndNullRoot()
    {
        using (var env = TestEnvironment.Create())
        {
            string customFrameworkDirToUse = Path.Combine(env.CreateFolder().Path, "framework-root");
            string searchPaths = $"/foo/bar;{customFrameworkDirToUse}";

            CheckGetPathToReferenceAssemblies(
                env,
                customFrameworkDirToUse,
                searchPaths,
                (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string fallbackSearchPaths)
                    => ToolLocationHelper.GetPathToReferenceAssemblies(
                        frameworkName, "v" + frameworkVersion, frameworkProfile, targetFrameworkRootPath: null, targetFrameworkFallbackSearchPaths: fallbackSearchPaths));
        }
    }

    // second overload of GetPathToReferenceAssemblies
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkInRoot2()
    {
        using (var env = TestEnvironment.Create())
        {
            string customFrameworkDirToUse = Path.Combine(env.CreateFolder().Path, "framework-root");
            CheckGetPathToReferenceAssemblies(
                env,
                customFrameworkDirToUse,
                null,
                (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string fallbackSearchPaths)
                    => ToolLocationHelper.GetPathToReferenceAssemblies(
                        customFrameworkDir, fallbackSearchPaths, new FrameworkNameVersioning(frameworkName, new Version(frameworkVersion), frameworkProfile)));
        }
    }

    // second overload of GetPathToReferenceAssemblies
    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkRootInFallbackPath2()
    {
        using (var env = TestEnvironment.Create())
        {
            string customFrameworkDirToUse = Path.Combine(env.CreateFolder().Path, "framework-root");
            string searchPaths = $"{customFrameworkDirToUse};/a/b";
            string rootDir = env.CreateFolder().Path;

            CheckGetPathToReferenceAssemblies(
                env,
                customFrameworkDirToUse,
                searchPaths,
                (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string fallbackSearchPaths)
                    => ToolLocationHelper.GetPathToReferenceAssemblies(
                        rootDir, fallbackSearchPaths, new FrameworkNameVersioning(frameworkName, new Version(frameworkVersion), frameworkProfile)));
        }
    }

    [Fact]
    public void GetPathToReferenceAssembliesWithCustomTargetFrameworkRootInFallbackPathAndNullRoot2()
    {
        using (var env = TestEnvironment.Create())
        {
            string customFrameworkDirToUse = Path.Combine(env.CreateFolder().Path, "framework-root");
            string searchPaths = $"{customFrameworkDirToUse};/a/b";
            CheckGetPathToReferenceAssemblies(
                env,
                customFrameworkDirToUse,
                searchPaths,
                (string frameworkName, string frameworkVersion, string frameworkProfile, string customFrameworkDir, string fallbackSearchPaths)
                    => ToolLocationHelper.GetPathToReferenceAssemblies(
                        null, fallbackSearchPaths, new FrameworkNameVersioning(frameworkName, new Version(frameworkVersion), frameworkProfile)));
        }
    }

    private void CheckGetPathToReferenceAssemblies(TestEnvironment env, string customFrameworkDir, string fallbackSearchPaths, Func<string, string, string, string, string, IList<string>> getPathToReferenceAssemblies)
    {
        string frameworkName = "Foo Framework";
        string frameworkVersion = "0.1";
        string frameworkVersionWithV = "v" + frameworkVersion;
        string frameworkProfile = string.Empty;

        string asmPath = CreateNewFrameworkAndGetAssembliesPath(env, frameworkName, frameworkVersionWithV, customFrameworkDir);

        var stdLibPaths = getPathToReferenceAssemblies(frameworkName, frameworkVersion, frameworkProfile, customFrameworkDir, fallbackSearchPaths);
        stdLibPaths.Count.ShouldBe(1);
        stdLibPaths[0].ShouldBe(Path.Combine(customFrameworkDir, frameworkName, frameworkVersionWithV) + Path.DirectorySeparatorChar, stdLibPaths[0]);
    }

    [Fact]
    public void GetPathToReferenceAssembliesWithNullTargetFrameworkRootPath()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        var v45Paths = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty);

        // This look up should fall back the default path with the .NET frameworks
        var v45PathsWithNullRoot = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty, null);

        v45PathsWithNullRoot.ShouldBe(v45Paths);
    }

    [Fact]
    public void GetPathToReferenceAssembliesWithNullTargetFrameworkFallbackSearchPaths()
    {
        string frameworkName = ".NETFramework";
        string frameworkVersion = "v4.5";

        var v45Paths = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty);

        // This look up should fall back the default path with the .NET frameworks
        var v45PathsWithNullRoot = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName, frameworkVersion, string.Empty, null, null);

        v45PathsWithNullRoot.ShouldBe(v45Paths);
    }

    private static string CreateNewFrameworkAndGetAssembliesPath(TestEnvironment env, string frameworkName, string frameworkVersion, string rootDir)
    {
        string frameworkListXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <FileList  Name=""{0}""/>";

        string redistPath = Path.Combine(rootDir, frameworkName, frameworkVersion, "RedistList");
        string asmPath = Path.Combine(rootDir, frameworkName, frameworkVersion);

        env.CreateFolder(redistPath);
        env.CreateFolder(asmPath);

        File.WriteAllText(Path.Combine(redistPath, "FrameworkList.xml"), string.Format(frameworkListXml, frameworkName));
        File.WriteAllText(Path.Combine(asmPath, "mscorlib.dll"), string.Empty);

        return asmPath;
    }
}
