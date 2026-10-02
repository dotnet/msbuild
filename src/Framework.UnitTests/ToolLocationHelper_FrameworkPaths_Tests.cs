// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
#if FEATURE_WIN32_REGISTRY
using Microsoft.Win32;
#endif
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    /// <summary>
    /// Verify the case where we ask for a tool using a target framework version of 3.5
    /// We make sure in the fake sdk path we also create a 4.0 folder in order to make sure we do not return that when we only want the bin directory.
    /// </summary>
    [Fact]
    public void VerifyinternalGetPathToDotNetFrameworkSdkFileNot40()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "VGPTDNFSFN40");
        string temp35Directory = Path.Combine(tempDirectory, "bin");
        string temp40Directory = Path.Combine(temp35Directory, "NETFX 4.0 Tools");
        string toolPath = Path.Combine(temp35Directory, "MyTool.exe");
        string toolPath40 = Path.Combine(temp40Directory, "MyTool.exe");

        try
        {
            if (!Directory.Exists(temp35Directory))
            {
                Directory.CreateDirectory(temp35Directory);
            }

            // Make a .NET 4.0 Tools so that we can make sure that we do not return it if we are not targeting 4.0
            if (!Directory.Exists(temp40Directory))
            {
                Directory.CreateDirectory(temp40Directory);
            }

            // Write a tool to disk to the existence check works
            File.WriteAllText(toolPath, "Contents");
            File.WriteAllText(toolPath40, "Contents");

            string foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", temp35Directory, "x86");
            foundToolPath.ShouldNotBeNull();
            foundToolPath.ShouldBe(toolPath, StringCompareShould.IgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(tempDirectory, true);
            }
        }
    }

    /// <summary>
    /// Verify the case where we ask for a tool using a target framework version of 4.0
    /// </summary>
    [Fact]
    public void VerifyinternalGetPathToDotNetFrameworkSdkFile40()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "VGPTDNFSF40");
        string temp35Directory = Path.Combine(tempDirectory, "bin");
        string temp40Directory = Path.Combine(temp35Directory, "NETFX 4.0 Tools");
        string toolPath = Path.Combine(temp35Directory, "MyTool.exe");
        string toolPath40 = Path.Combine(temp40Directory, "MyTool.exe");

        try
        {
            if (!Directory.Exists(temp35Directory))
            {
                Directory.CreateDirectory(temp35Directory);
            }

            // Make a .NET 4.0 Tools so that we can make sure that we do not return it if we are not targeting 4.0
            if (!Directory.Exists(temp40Directory))
            {
                Directory.CreateDirectory(temp40Directory);
            }

            // Write a tool to disk to the existence check works
            File.WriteAllText(toolPath, "Contents");
            File.WriteAllText(toolPath40, "Contents");

            string foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", temp40Directory, "x86");
            foundToolPath.ShouldNotBeNull();
            foundToolPath.ShouldBe(toolPath40, StringCompareShould.IgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(tempDirectory, true);
            }
        }
    }

    /// <summary>
    /// Make sure if null is passed in for any of the arguments that the method returns null and does not crash.
    /// </summary>
    [Fact]
    public void VerifyinternalGetPathToDotNetFrameworkSdkFileNullPassedIn()
    {
        string foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", "C:\\Path", null);
        foundToolPath.ShouldBeNull();

        foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", null, "x86");
        foundToolPath.ShouldBeNull();

        foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(null, "c:\\path", "x86");
        foundToolPath.ShouldBeNull();
    }

    /*
      * Method:   FindFrameworksPathRunningThisTest
      *
      * Our FX path should be resolved as the one we're running on by default
      */
    [Fact]
    public void FindFrameworksPathRunningThisTest()
    {
        string path = FrameworkLocationHelper.FindDotNetFrameworkPath(
            Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName),
            ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version40),
            DirectoryExists,
            GetDirectories,
            DotNetFrameworkArchitecture.Current);

#if FEATURE_INSTALLED_MSBUILD
        path.ShouldBe(Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName));
#else
        path.ShouldBeNull();
#endif
    }

    /*
     * Method:   FindFrameworksPathRunningUnderWhidbey
     *
     * Search for a whidbey when whidbey is the current version.
     */
    [WindowsOnlyFact]
    public void FindFrameworksPathRunningUnderWhidbey()
    {
        string path = FrameworkLocationHelper.FindDotNetFrameworkPath(
                @"{runtime-base}\v1.2.x86dbg",    // Simulate "Whidbey" as the current runtime.
                "v1.2",
                DirectoryExists,
                GetDirectories,
                DotNetFrameworkArchitecture.Current);
        path.ShouldBe(Path.Combine("{runtime-base}", "v1.2.x86dbg"));
    }

    /*
    * Method:   FindFrameworksPathRunningUnderOrcas
    *
    * Search for a whidbey when orcas is the current version.
    */
    [WindowsOnlyFact]
    public void FindFrameworksPathRunningUnderOrcas()
    {
        string path = FrameworkLocationHelper.FindDotNetFrameworkPath(
                Path.Combine("{runtime-base}", "v1.3.x86dbg"),        // Simulate "Orcas" as the current runtime.}
                "v1.2",                                              // But we're looking for "Whidbey"
                DirectoryExists,
                GetDirectories,
                DotNetFrameworkArchitecture.Current);
        path.ShouldBe(Path.Combine("{runtime-base}", "v1.2.x86fre"));
    }

    /*
    * Method:   FindFrameworksPathRunningUnderEverett
    *
    * Search for a whidbey when orcas is the current version.
    */
    [WindowsOnlyFact]
    public void FindFrameworksPathRunningUnderEverett()
    {
        string path = FrameworkLocationHelper.FindDotNetFrameworkPath(
                Path.Combine("{runtime-base}", "v1.1.x86dbg"),       // Simulate "Everett" as the current runtime.
                "v1.2",                                              // But we're looking for "Whidbey"
                DirectoryExists,
                GetDirectories,
                DotNetFrameworkArchitecture.Current);

        path.ShouldBe(Path.Combine("{runtime-base}", "v1.2.x86fre"));
    }

    /*
    * Method:   FindPathForNonexistentFrameworks
    *
    * Trying to find a non-existent path should return null.
    */
    [Fact]
    public void FindPathForNonexistentFrameworks()
    {
        string path = FrameworkLocationHelper.FindDotNetFrameworkPath(
                Path.Combine(@"{runtime-base}", "v1.1"), // Simulate "everett" as the current runtime
                "v1.3",                                  // And we're trying to find "orcas" runtime which isn't installed.
                DirectoryExists,
                GetDirectories,
                DotNetFrameworkArchitecture.Current);

        path.ShouldBeNull();
    }

    /*
    * Method:   FindPathForEverettThatIsntProperlyInstalled
    *
    * Trying to find a path if GetRequestedRuntimeInfo fails and useHeuristic=false should return null.
    */
    [Fact]
    public void FindPathForEverettThatIsntProperlyInstalled()
    {
        string tempPath = Path.GetTempPath();
        string fakeWhidbeyPath = Path.Combine(tempPath, "v2.0.50224");
        string fakeEverettPath = Path.Combine(tempPath, "v1.1.43225");
        Directory.CreateDirectory(fakeEverettPath);

        string path = FrameworkLocationHelper.FindDotNetFrameworkPath(
            fakeWhidbeyPath,  // Simulate "whidbey" as the current runtime
            "v1.1",           // We're looking for "everett"
            DirectoryExists,
            GetDirectories,
            DotNetFrameworkArchitecture.Current);

        FileUtilities.DeleteWithoutTrailingBackslash(fakeEverettPath);
        path.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void ExerciseMiscToolLocationHelperMethods()
    {
        ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version11).ShouldBe(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV11);
        ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version20).ShouldBe(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV20);
        ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version30).ShouldBe(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV30);
        ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version35).ShouldBe(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV35);
        ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version40).ShouldBe(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV40);
        ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.VersionLatest).ShouldBe(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV40);
        ToolLocationHelper.GetDotNetFrameworkRootRegistryKey(TargetDotNetFrameworkVersion.VersionLatest).ShouldBe(FrameworkLocationHelper.fullDotNetFrameworkRegistryKey);
        ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Latest).ShouldBe(FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV40);
        ToolLocationHelper.GetDotNetFrameworkRootRegistryKey(TargetDotNetFrameworkVersion.Latest).ShouldBe(FrameworkLocationHelper.fullDotNetFrameworkRegistryKey);

        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkV11);
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkV20);
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkV30);
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkV35);
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkV40);
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.VersionLatest).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkV40);
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Latest).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkV40);

        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV11(DotNetFrameworkArchitecture.Bitness32));
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Bitness32));
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV30(DotNetFrameworkArchitecture.Bitness32));
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Bitness32));

        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture.Bitness32));
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.VersionLatest, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture.Bitness32));
        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Latest, DotNetFrameworkArchitecture.Bitness32).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture.Bitness32));

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)")))
        {
            // 64-bit machine, so we should test the 64-bit overloads as well
            ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11, DotNetFrameworkArchitecture.Bitness64).ShouldBe(
            FrameworkLocationHelper.GetPathToDotNetFrameworkV11(DotNetFrameworkArchitecture.Bitness64));
            ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20, DotNetFrameworkArchitecture.Bitness64).ShouldBe(
                FrameworkLocationHelper.GetPathToDotNetFrameworkV20(DotNetFrameworkArchitecture.Bitness64));
            ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30, DotNetFrameworkArchitecture.Bitness64).ShouldBe(
                FrameworkLocationHelper.GetPathToDotNetFrameworkV30(DotNetFrameworkArchitecture.Bitness64));
            ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35, DotNetFrameworkArchitecture.Bitness64).ShouldBe(
                FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Bitness64));

            ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40, DotNetFrameworkArchitecture.Bitness64).ShouldBe(
                FrameworkLocationHelper.GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture.Bitness64));
            ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.VersionLatest, DotNetFrameworkArchitecture.Bitness64).ShouldBe(
                FrameworkLocationHelper.GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture.Bitness64));
            ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Latest, DotNetFrameworkArchitecture.Bitness64).ShouldBe(
                FrameworkLocationHelper.GetPathToDotNetFrameworkV40(DotNetFrameworkArchitecture.Bitness64));
        }
    }

    [Fact]
    public void InvalidDotNetFrameworkArchitectureThrows()
    {
        var architecture = (DotNetFrameworkArchitecture)int.MaxValue;

        Should.Throw<InternalErrorException>(
            () => ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40, architecture));
        Should.Throw<InternalErrorException>(
            () => ToolLocationHelper.GetPathToBuildTools(ToolLocationHelper.CurrentToolsVersion, architecture));
    }

    [Fact]
    public void TestGetDotNetFrameworkSdkRootRegistryKey()
    {
        // Test out of range .net version.
        foreach (var vsVersion in EnumVisualStudioVersions())
        {
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey((TargetDotNetFrameworkVersion)99, vsVersion));
        }

        // Test out of range visual studio version.
        foreach (var dotNetVersion in EnumDotNetFrameworkVersions())
        {
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(dotNetVersion, (VisualStudioVersion)99));
        }

        foreach (var vsVersion in EnumVisualStudioVersions())
        {
            // v1.1
            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version11, vsVersion).ShouldBe(FrameworkLocationHelper.fullDotNetFrameworkRegistryKey);

            // v2.0
            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version20, vsVersion).ShouldBe(FrameworkLocationHelper.fullDotNetFrameworkRegistryKey);

            // v3.0
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version30, vsVersion));

            // v3.5
            ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version35, vsVersion).ShouldBe(
                vsVersion == VisualStudioVersion.Version100 ? FrameworkLocationHelper.fullDotNetFrameworkSdkRegistryKeyV35OnVS10 : FrameworkLocationHelper.fullDotNetFrameworkSdkRegistryKeyV35OnVS11);
        }

        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK70A = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.0A\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0A\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.1A\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.6\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK461 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.6.1\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK462 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.6.2\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK47 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.7\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK471 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.7.1\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK472 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.7.2\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK48 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.8\WinSDK-NetFx40Tools-x86";
        string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK481 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.8.1\WinSDK-NetFx40Tools-x86";

        // v4.0
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version100).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK70A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version110).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version120).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version140).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

        // v4.5
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version100).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version110).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version120).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version140).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

        // v4.5.1
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version110));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version120).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version140).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

        // v4.5.2
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version110));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version120).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version140).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

        // v4.6
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version120));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version140).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

        // v4.6.1
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version120));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version140).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK461);

        // v4.6.2
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version120));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version150).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK462);

        // v4.7
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version47, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version47, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version47, VisualStudioVersion.Version120));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version47, VisualStudioVersion.Version150).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK47);

        // v4.7.1
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version471, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version471, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version471, VisualStudioVersion.Version120));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version471, VisualStudioVersion.Version150).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK471);

        // v4.7.2
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version472, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version472, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version472, VisualStudioVersion.Version120));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version472, VisualStudioVersion.Version150).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK472);

        // v4.8
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version48, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version48, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version48, VisualStudioVersion.Version120));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version48, VisualStudioVersion.Version140));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version48, VisualStudioVersion.Version150).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK48);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version48, VisualStudioVersion.Version160).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK48);
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version48, VisualStudioVersion.Version170).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK48);

        // v4.8.1
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version481, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version481, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version481, VisualStudioVersion.Version120));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version481, VisualStudioVersion.Version140));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version481, VisualStudioVersion.Version150));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version481, VisualStudioVersion.Version160));
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version481, VisualStudioVersion.Version170).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK481);

        // Latest
        ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Latest, VisualStudioVersion.Version170).ShouldBe(fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK481);
    }

    [Fact]
    public void TestGetDotNetFrameworkSdkInstallKeyValue()
    {
        // Test out of range .net version.
        foreach (var vsVersion in EnumVisualStudioVersions())
        {
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue((TargetDotNetFrameworkVersion)99, vsVersion));
        }

        // Test out of range visual studio version.
        foreach (var dotNetVersion in EnumDotNetFrameworkVersions())
        {
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(dotNetVersion, (VisualStudioVersion)99));
        }

        string InstallationFolder = "InstallationFolder";

        foreach (var vsVersion in EnumVisualStudioVersions())
        {
            // v1.1
            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version11, vsVersion).ShouldBe(FrameworkLocationHelper.dotNetFrameworkSdkInstallKeyValueV11);

            // v2.0
            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version20, vsVersion).ShouldBe(FrameworkLocationHelper.dotNetFrameworkSdkInstallKeyValueV20);

            // v3.0
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version30, vsVersion));

            // v3.5
            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version35, vsVersion).ShouldBe(InstallationFolder);

            // v4.0
            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version40, vsVersion).ShouldBe(InstallationFolder);

            // v4.5
            ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version45, vsVersion).ShouldBe(InstallationFolder);
        }

        // v4.5.1
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version110));
        ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version120).ShouldBe(InstallationFolder);
        ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version140).ShouldBe(InstallationFolder);

        // v4.6
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version120));
        ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version140).ShouldBe(InstallationFolder);
    }

#if FEATURE_REGISTRY_SDKS
    [WindowsOnlyFact(Skip = "https://github.com/dotnet/msbuild/issues/995")]
#else
    [WindowsOnlyFact(Skip = "Registry SDKs not supported")]
#endif
    public void GetPathToDotNetFrameworkSdk()
    {
        // Test out of range .net version.
        foreach (var vsVersion in EnumVisualStudioVersions())
        {
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk((TargetDotNetFrameworkVersion)99, vsVersion));
        }

        // Test out of range visual studio version.
        foreach (var dotNetVersion in EnumDotNetFrameworkVersions())
        {
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk(dotNetVersion, (VisualStudioVersion)99));
        }

        string pathToSdk35InstallRoot = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v7.0A\");
        string pathToSdkV4InstallRootOnVS10 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v7.0A\");
        string pathToSdkV4InstallRootOnVS11 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v8.0A\");

        // After uninstalling the 4.5 (Dev11) SDK, the Bootstrapper folder is left behind, so we can't
        // just check for the root folder.
        if (!Directory.Exists(Path.Combine(pathToSdkV4InstallRootOnVS11, "bin")))
        {
            // falls back to the Dev10 location (7.0A)
            pathToSdkV4InstallRootOnVS11 = pathToSdkV4InstallRootOnVS10;
        }

        string pathToSdkV4InstallRootOnVS12 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v8.1A\");

        if (!Directory.Exists(pathToSdkV4InstallRootOnVS12))
        {
            // falls back to the Dev11 location (8.0A)
            pathToSdkV4InstallRootOnVS12 = pathToSdkV4InstallRootOnVS11;
        }

        string pathToSdkV4InstallRootOnVS14 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v10.0A\");

        foreach (var vsVersion in EnumVisualStudioVersions())
        {
            // v1.1
            ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version11, vsVersion).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkSdkV11);

            // v2.0
            ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version20, vsVersion).ShouldBe(FrameworkLocationHelper.PathToDotNetFrameworkSdkV20);

            // v3.0
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version30, vsVersion));

            // v3.5
            ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, vsVersion).ShouldBe(pathToSdk35InstallRoot);
        }

        // v4.0
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version100).ShouldBe(pathToSdkV4InstallRootOnVS10);
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version110).ShouldBe(pathToSdkV4InstallRootOnVS11);
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version120).ShouldBe(pathToSdkV4InstallRootOnVS12);
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version140).ShouldBe(pathToSdkV4InstallRootOnVS14);

        // v4.5
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version100).ShouldBe(pathToSdkV4InstallRootOnVS11);
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version110).ShouldBe(pathToSdkV4InstallRootOnVS11);
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version120).ShouldBe(pathToSdkV4InstallRootOnVS12);
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version140).ShouldBe(pathToSdkV4InstallRootOnVS14);

        // v4.5.1
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version110));
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version120).ShouldBe(pathToSdkV4InstallRootOnVS12);
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version140).ShouldBe(pathToSdkV4InstallRootOnVS14);

        // v4.6
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version100));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version110));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version120));
        ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version140).ShouldBe(pathToSdkV4InstallRootOnVS14);
    }

#pragma warning disable 618 //The test below tests a deprecated API. We disable the warning for obsolete methods for this particular test
#if FEATURE_WIN32_REGISTRY
    [WindowsOnlyFact]
    public void GetPathToWindowsSdk()
    {
        // Test out of range .net version.
        foreach (var vsVersion in EnumVisualStudioVersions())
        {
            Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToWindowsSdk((TargetDotNetFrameworkVersion)99, vsVersion));
        }

        string pathToWindowsSdkV80 = GetRegistryValueHelper(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0", "InstallationFolder");
        string pathToWindowsSdkV81 = GetRegistryValueHelper(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.1", "InstallationFolder");

        foreach (var vsVersion in EnumVisualStudioVersions().Concat(new[] { (VisualStudioVersion)99 }))
        {
            // v1.1, v2.0, v3.0, v3.5, v4.0
            foreach (var dotNetVersion in EnumDotNetFrameworkVersions().Where(v => v <= TargetDotNetFrameworkVersion.Version40))
            {
                Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPathToWindowsSdk(dotNetVersion, vsVersion));
            }

            // v4.5
            ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version45, vsVersion).ShouldBe(pathToWindowsSdkV80);

            // v4.5.1
            ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version451, vsVersion).ShouldBe(pathToWindowsSdkV81);

            // v4.6
            ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version46, vsVersion).ShouldBe(pathToWindowsSdkV81);
        }
    }
#endif

#pragma warning restore 618

    /*
    * Method:   GetDirectories
    *
    * Delegate method simulates a file system for testing location methods.
    */
    private static string[] GetDirectories(string path, string pattern)
        => path == "{runtime-base}" && pattern == "v1.2*"
            ? [@"{runtime-base}\v1.2.30617", @"{runtime-base}\v1.2.x86dbg", @"{runtime-base}\v1.2.x86fre"]
            : [];

    /// <summary>
    /// Delegate method simulates a file system for testing location methods.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool DirectoryExists(string path)
        => path.Contains("{runtime-base}") || Directory.Exists(path);

#if FEATURE_WIN32_REGISTRY
    private static string GetRegistryValueHelper(RegistryHive hive, RegistryView view, string subKeyPath, string name)
    {
        using (var key = RegistryHelper.OpenBaseKey(hive, view))
        using (var subKey = key.OpenSubKey(subKeyPath))
        {
            if (subKey != null)
            {
                return (string)subKey.GetValue(name);
            }
        }

        return null;
    }
#endif

    private static IEnumerable<VisualStudioVersion> EnumVisualStudioVersions()
    {
        for (VisualStudioVersion vsVersion = VisualStudioVersion.Version100; vsVersion <= VisualStudioVersion.VersionLatest; ++vsVersion)
        {
            yield return vsVersion;
        }
    }

    private static IEnumerable<TargetDotNetFrameworkVersion> EnumDotNetFrameworkVersions()
    {
        for (TargetDotNetFrameworkVersion dotNetVersion = TargetDotNetFrameworkVersion.Version11; dotNetVersion <= TargetDotNetFrameworkVersion.VersionLatest; ++dotNetVersion)
        {
            yield return dotNetVersion;
        }
    }
}
