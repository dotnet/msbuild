// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
#if FEATURE_WIN32_REGISTRY
using Microsoft.Win32;
#endif
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public partial class GetPlatformExtensionSDKLocationsTestFixture
{
    /// <summary>
    /// Pass empty and null target platform identifier and target platform version string to make sure we get the correct exceptions out.
    /// </summary>
    [Fact]
    public void PassEmptyAndNullTPM()
    {
        VerifyExceptionOnEmptyOrNullPlatformAttributes(string.Empty, new Version("1.0"));
        VerifyExceptionOnEmptyOrNullPlatformAttributes(null, new Version("1.0"));
        VerifyExceptionOnEmptyOrNullPlatformAttributes(null, null);
        VerifyExceptionOnEmptyOrNullPlatformAttributes("Windows", null);
    }

    /// <summary>
    /// Verify that we get argument exceptions where different combinations of identifier and version are passed in.
    /// </summary>
    private static void VerifyExceptionOnEmptyOrNullPlatformAttributes(string identifier, Version version)
    {
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPlatformExtensionSDKLocations(identifier, version));
        Should.Throw<ArgumentException>(() => ToolLocationHelper.GetPlatformSDKLocation(identifier, version));
    }

    /// <summary>
    /// Verify we can get a list of extension sdks out of the API
    /// </summary>
    [Fact]
    public void TestGetExtensionSDKLocations()
    {
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

            // Identifier does not exist
            IDictionary<string, string> sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations([_fakeStructureRoot], null, "FOO", new Version(1, 0));
            sdks.Count.ShouldBe(0);

            // Identifier exists
            sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations([_fakeStructureRoot], null, "MyPlatform", new Version(3, 0));
            sdks.ShouldContainKey("MyAssembly, Version=1.0");
            sdks.Count.ShouldBe(1);

            // Targeting version higher than exists, however since we are using a russian doll model for extension sdks we will return ones in lower versions of the targeted platform.
            sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations([_fakeStructureRoot], null, "MyPlatform", new Version(4, 0));
            sdks.ShouldContainKey("MyAssembly, Version=1.0");
            sdks["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "3.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
            sdks.ShouldContainKey("AnotherAssembly, Version=1.0");
            sdks["AnotherAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "4.0", "ExtensionSDKs", "AnotherAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
            sdks.Count.ShouldBe(2);

            // Identifier exists but no extensions are in sdks this version or lower
            sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations([_fakeStructureRoot], null, "MyPlatform", new Version(1, 0));
            sdks.Count.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
        }
    }

    /// <summary>
    /// Verify we can get a single extension sdk location out of the API
    /// </summary>
    [Fact]
    public void TestGetExtensionSDKLocation()
    {
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

            // Identifier does not exist
            IDictionary<string, string> sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations([_fakeStructureRoot], null, "FOO", new Version(1, 0));
            sdks.Count.ShouldBe(0);

            string targetPath =
                Path.Combine(_fakeStructureRoot, "MyPlatform", "3.0", "ExtensionSDKs", "MyAssembly", "1.0")
                + Path.DirectorySeparatorChar;

            // Identifier exists
            string path = ToolLocationHelper.GetPlatformExtensionSDKLocation(
                "MyAssembly, Version=1.0",
                "MyPlatform",
                new Version(3, 0),
                [_fakeStructureRoot],
                null);
            path.ShouldBe(targetPath, StringCompareShould.IgnoreCase);

            // Identifier exists in lower version
            path = ToolLocationHelper.GetPlatformExtensionSDKLocation(
                "MyAssembly, Version=1.0",
                "MyPlatform",
                new Version(4, 0),
                [_fakeStructureRoot],
                null);
            path.ShouldBe(targetPath, StringCompareShould.IgnoreCase);

            // Identifier does not exist
            path = ToolLocationHelper.GetPlatformExtensionSDKLocation("Something, Version=1.0", "MyPlatform", new Version(4, 0), new[] { _fakeStructureRoot }, null);
            path.Length.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
        }
    }

    /// <summary>
    /// Verify Extension SDKs are filtered correctly
    /// </summary>
    [Fact]
    public void VerifyFilterPlatformExtensionSdks()
    {
        // Create fake directory tree
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "True");

            IDictionary<string, string> extensionSDKs = ToolLocationHelper.GetPlatformExtensionSDKLocations([_fakeStructureRoot], null, "MyPlatform", new Version(4, 0));
            IDictionary<string, string> filteredExtensionSDKs1 = ToolLocationHelper.FilterPlatformExtensionSDKs(new Version(8, 0), extensionSDKs);
            IDictionary<string, string> filteredExtensionSDKs2 = ToolLocationHelper.FilterPlatformExtensionSDKs(new Version(9, 0), extensionSDKs);
            IDictionary<string, string> filteredExtensionSDKs3 = ToolLocationHelper.FilterPlatformExtensionSDKs(new Version(10, 0), extensionSDKs);

            filteredExtensionSDKs1.Count.ShouldBe(2);
            filteredExtensionSDKs2.Count.ShouldBe(1);
            filteredExtensionSDKs3.Count.ShouldBe(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
        }
    }

    /// <summary>
    /// Verify that the GetPlatformExtensionSDKLocation method can be correctly called during evaluation time as a msbuild function.
    /// </summary>
    [Fact]
    public void VerifyGetInstalledSDKLocations()
    {
        string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetInstalledSDKLocations");
        string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform", "8.0")
                                   + Path.DirectorySeparatorChar;
        string sdkDirectory = Path.Combine(platformDirectory, "ExtensionSDKs", "SDkWithManifest", "2.0")
                              + Path.DirectorySeparatorChar;

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0'))</SDKLocation1>
                    <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0'))</SDKLocation2>
                    <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0'))</SDKLocation3>
                    <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0'))</SDKName>
                 </PropertyGroup>

                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

        try
        {
            Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", testDirectoryRoot);
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }

            Directory.CreateDirectory(testDirectoryRoot);
            Directory.CreateDirectory(sdkDirectory);
            File.WriteAllText(Path.Combine(platformDirectory, "SDKManifest.xml"), "HI");
            File.WriteAllText(Path.Combine(sdkDirectory, "SDKManifest.xml"), "HI");
            string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

            File.WriteAllText(testProjectFile, tempProjectContents);

            using ProjectCollection pc = new ProjectCollection();
            Project project = pc.LoadProject(testProjectFile);
            string propertyValue1 = project.GetPropertyValue("SDKLocation1");
            string propertyValue2 = project.GetPropertyValue("SDKLocation2");
            string propertyValue3 = project.GetPropertyValue("SDKLocation3");
            string sdkName = project.GetPropertyValue("SDKName");

            propertyValue1.ShouldBe(sdkDirectory, StringCompareShould.IgnoreCase);
            propertyValue2.Length.ShouldBe(0);
            propertyValue3.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);

            // No displayname set in the SDK manifest, so it mocks one up
            sdkName.ShouldBe("MyPlatform 8.0");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }
        }
    }

    /// <summary>
    /// Verify that the GetPlatformExtensionSDKLocation method can be correctly called during evaluation time as a msbuild function.
    /// </summary>
    [Fact]
    public void VerifyGetInstalledSDKLocations2()
    {
        string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetInstalledSDKLocations2");
        string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform", "8.0")
                                   + Path.DirectorySeparatorChar;
        string sdkDirectory = Path.Combine(platformDirectory, "ExtensionSDKs", "SDkWithManifest", "2.0")
                              + Path.DirectorySeparatorChar;

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>" +
               @"<SDKDirectoryRoot>" + testDirectoryRoot + "</SDKDirectoryRoot>" +
                @"<SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation1>
                      <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation2>
                      <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation3>
                      <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0', '$(SDKDirectoryRoot)', ''))</SDKName>
                 </PropertyGroup>

                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

        string platformSDKManifestContents = @"<FileList
                    DisplayName = ""My cool platform SDK!"">
                </FileList>";

        try
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }

            Directory.CreateDirectory(testDirectoryRoot);
            Directory.CreateDirectory(sdkDirectory);
            File.WriteAllText(Path.Combine(platformDirectory, "SDKManifest.xml"), platformSDKManifestContents);
            File.WriteAllText(Path.Combine(sdkDirectory, "SDKManifest.xml"), "HI");
            string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

            File.WriteAllText(testProjectFile, tempProjectContents);

            using ProjectCollection pc = new ProjectCollection();
            Project project = pc.LoadProject(testProjectFile);
            string propertyValue1 = project.GetPropertyValue("SDKLocation1");
            string propertyValue2 = project.GetPropertyValue("SDKLocation2");
            string propertyValue3 = project.GetPropertyValue("SDKLocation3");
            string sdkName = project.GetPropertyValue("SDKName");

            propertyValue1.ShouldBe(sdkDirectory, StringCompareShould.IgnoreCase);
            propertyValue3.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
            propertyValue2.Length.ShouldBe(0);
            sdkName.ShouldBe("My cool platform SDK!");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }
        }
    }

#if FEATURE_REGISTRY_SDKS
    /// <summary>
    /// Setup some fake entries in the registry and verify we get the correct sdk from there.
    /// </summary>
    [Fact]
    public void VerifyGetInstalledSDKLocations3()
    {
        string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetInstalledSDKLocations3");
        string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform", "8.0")
                                   + Path.DirectorySeparatorChar;
        string sdkDirectory = Path.Combine(platformDirectory, "ExtensionSDKs", "SDkWithManifes", "2.0")
                              + Path.DirectorySeparatorChar;

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>SOFTWARE\Microsoft\VerifyGetInstalledSDKLocations3</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation1>
                    <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation2>
                    <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation3>
                    <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</SDKName>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

        string platformSDKManifestContents = @"<FileList
                    DisplayName = ""MyPlatform from the registry""
                    PlatformIdentity = ""MyPlatform, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""12.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""MyPlatform, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
            </FileList>";

        string registryKey = @"SOFTWARE\Microsoft\VerifyGetInstalledSDKLocations3\";
        RegistryKey baseKey = Registry.CurrentUser;

        try
        {
            if (NativeMethodsShared.IsWindows)
            {
                RegistryKey folderKey = baseKey.CreateSubKey(registryKey + @"\MyPlatform\v8.0\ExtensionSDKS\SDKWithManifest\2.0");
                folderKey.SetValue("", Path.Combine(testDirectoryRoot, sdkDirectory));

                folderKey = baseKey.CreateSubKey(registryKey + @"\MyPlatform\v8.0");
                folderKey.SetValue("", Path.Combine(testDirectoryRoot, platformDirectory));
            }

            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }

            Directory.CreateDirectory(testDirectoryRoot);
            Directory.CreateDirectory(sdkDirectory);
            File.WriteAllText(Path.Combine(sdkDirectory, "SDKManifest.xml"), "HI");
            File.WriteAllText(Path.Combine(platformDirectory, "SDKManifest.xml"), platformSDKManifestContents);

            string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

            File.WriteAllText(testProjectFile, tempProjectContents);

            using ProjectCollection pc = new ProjectCollection();
            Project project = pc.LoadProject(testProjectFile);
            string propertyValue1 = project.GetPropertyValue("SDKLocation1");
            string propertyValue2 = project.GetPropertyValue("SDKLocation2");
            string propertyValue3 = project.GetPropertyValue("SDKLocation3");
            string sdkName = project.GetPropertyValue("SDKName");

            propertyValue1.ShouldBe(sdkDirectory, StringCompareShould.IgnoreCase);
            propertyValue3.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
            propertyValue2.Length.ShouldBe(0);
            sdkName.ShouldBe("MyPlatform from the registry");
        }
        finally
        {
            try
            {
                if (NativeMethodsShared.IsWindows)
                {
                    baseKey.DeleteSubKeyTree(registryKey);
                }
            }
            catch (Exception)
            {
            }
            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }
        }
    }
#endif
}
