// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
#if FEATURE_WIN32_REGISTRY
using Microsoft.Win32;
#endif
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed class GetPlatformExtensionSDKLocationsIntegration_Tests
{
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

#if FEATURE_REGISTRY_SDKS
    /// <summary>
    /// Verify that the GetPlatformSDKPropsFileLocation method can be correctly called for pre-OneCore SDKs during evaluation time as a msbuild function.
    /// </summary>
    [Fact]
    public void VerifyGetPreOneCoreSDKPropsLocation()
    {
        // This is the mockup layout for SDKs before One Core SDK.
        string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetPreOneCoreSDKPropsLocation");
        string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform", "8.0")
                                   + Path.DirectorySeparatorChar;
        string propsDirectory = Path.Combine(platformDirectory, "DesignTime", "CommonConfiguration", "Neutral");

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>SOFTWARE\Microsoft\VerifyGetPlatformSDKPropsLocation</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <PlatformSDKLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform', '8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</PlatformSDKLocation>
                    <PropsLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKPropsFileLocation('',' ','MyPlatform',' ','8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</PropsLocation>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

        string registryKey = @"SOFTWARE\Microsoft\VerifyGetPlatformSDKPropsLocation\";
        RegistryKey baseKey = Registry.CurrentUser;

        try
        {
            if (NativeMethodsShared.IsWindows)
            {
                using (RegistryKey platformKey = baseKey.CreateSubKey(registryKey + @"\MyPlatform\v8.0"))
                {
                    platformKey.SetValue("InstallationFolder", platformDirectory);
                }
            }

            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }

            Directory.CreateDirectory(testDirectoryRoot);
            Directory.CreateDirectory(propsDirectory);

            File.WriteAllText(Path.Combine(platformDirectory, "SDKManifest.xml"), "Test");

            using var collection = new ProjectCollection();
            Project project = ObjectModelHelpers.CreateInMemoryProject(collection, tempProjectContents);

            string propertyValue = project.GetPropertyValue("PlatformSDKLocation");
            string propsLocation = project.GetPropertyValue("PropsLocation");

            propertyValue.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
            propsLocation.ShouldBe(propsDirectory, StringCompareShould.IgnoreCase);
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

    /// <summary>
    /// Verify that the GetPlatformSDKPropsFileLocation method can be correctly called for OneCore SDK during evaluation time as a msbuild function.
    /// </summary>
    [Fact]
    public void VerifyGetOneCoreSDKPropsLocation()
    {
        // This is the mockup layout for One Core SDK.
        string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetOneCoreSDKPropsLocation");
        string platformDirectory = Path.Combine(testDirectoryRoot, "OneCoreSDK", "1.0") + Path.DirectorySeparatorChar;
        string propsDirectory =
            Path.Combine(platformDirectory, "DesignTime", "CommonConfiguration", "Neutral", "MyPlatform", "0.8.0.0");
        string platformDirectory2 = Path.Combine(platformDirectory, "Platforms", "MyPlatform", "0.8.0.0");

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>SOFTWARE\Microsoft\VerifyGetOneCoreSDKPropsLocation</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <PlatformSDKLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('OneCoreSDK', '1.0', '', '$(SDKRegistryRoot)'))</PlatformSDKLocation>
                    <PropsLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKPropsFileLocation('OneCoreSDK','1.0','MyPlatform',' ','0.8.0.0', '', '$(SDKRegistryRoot)'))</PropsLocation>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

        string registryKey = @"SOFTWARE\Microsoft\VerifyGetOneCoreSDKPropsLocation\";
        RegistryKey baseKey = Registry.CurrentUser;

        try
        {
            if (NativeMethodsShared.IsWindows)
            {
                using (RegistryKey platformKey = baseKey.CreateSubKey(registryKey + @"\OneCoreSDK\1.0"))
                {
                    platformKey.SetValue("InstallationFolder", platformDirectory);
                }
            }

            if (Directory.Exists(testDirectoryRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
            }

            Directory.CreateDirectory(testDirectoryRoot);
            Directory.CreateDirectory(propsDirectory);
            Directory.CreateDirectory(platformDirectory2);

            File.WriteAllText(Path.Combine(platformDirectory, "SDKManifest.xml"), "Test");
            File.WriteAllText(Path.Combine(platformDirectory2, "Platform.xml"), "Test");

            using var collection = new ProjectCollection();
            Project project = ObjectModelHelpers.CreateInMemoryProject(collection, tempProjectContents);

            string propertyValue = project.GetPropertyValue("PlatformSDKLocation");
            string propsLocation = project.GetPropertyValue("PropsLocation");

            propertyValue.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
            propsLocation.ShouldBe(propsDirectory, StringCompareShould.IgnoreCase);
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
