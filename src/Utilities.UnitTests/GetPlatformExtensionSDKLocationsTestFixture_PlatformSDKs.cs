// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Verify Platform SDKs are filtered correctly
    /// </summary>
    [Fact]
    public void VerifyFilterPlatformSdks()
    {
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "True");

            IList<TargetPlatformSDK> sdkList = ToolLocationHelper.GetTargetPlatformSdks([_fakeStructureRoot], null);
            IList<TargetPlatformSDK> filteredSdkList = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 5), new Version(12, 0));
            IList<TargetPlatformSDK> filteredSdkList1 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 1), new Version(10, 0));
            IList<TargetPlatformSDK> filteredSdkList2 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 3), new Version(10, 0));
            IList<TargetPlatformSDK> filteredSdkList3 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 3), new Version(11, 0));

            // Filter based only on OS version
            IList<TargetPlatformSDK> filteredSdkList4 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 3), null);

            // Filter based only on VS version
            IList<TargetPlatformSDK> filteredSdkList5 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, null, new Version(10, 0));

            // Pass both versions as null. Don't filter anything
            IList<TargetPlatformSDK> filteredSdkList6 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, null, null);

            sdkList.Count.ShouldBe(7);
            filteredSdkList.Count.ShouldBe(7);
            filteredSdkList1.Count.ShouldBe(2);
            filteredSdkList2.Count.ShouldBe(3);
            filteredSdkList3.Count.ShouldBe(4);
            filteredSdkList4.Count.ShouldBe(5);
            filteredSdkList5.Count.ShouldBe(5);
            filteredSdkList6.Count.ShouldBe(7);

            filteredSdkList2[0].TargetPlatformIdentifier.ShouldBe("MyPlatform");
            filteredSdkList2[2].TargetPlatformVersion.ShouldBe(new Version(3, 0));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
        }
    }

    /// <summary>
    /// sdk identifier we can't get any platforms back.
    /// </summary>
    [Fact]
    public void GetPlatformsForSDKNullSDKIdentifier()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPlatformsForSDK(null, new Version("1.0")));

    /// <summary>
    /// Make sure if the sdk version is null we get an ArgumentNullException because without specifying the
    /// sdk version we can't get any platforms back.
    /// </summary>
    [Fact]
    public void GetPlatformsForSDKNullSDKVersion()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetPlatformsForSDK("AnySDK", null));

    /// <summary>
    /// Verify that when there are no sdks with target platforms installed, our list of platforms is empty
    /// to make sure we are not getting platforms from somewhere else.
    /// </summary>
    [Fact]
    public void GetPlatformsForSDKWithNoInstalledTargetPlatforms()
        => ToolLocationHelper.GetPlatformsForSDK("AnySDK", new Version("1.0"), [], "").Any().ShouldBeFalse();

    /// <summary>
    /// Verify that the list of platforms returned is exactly as we expect when we have platforms
    /// installed and we pass in a matching sdk identifier and version number for one of the
    /// installed platforms.
    /// </summary>
    [Fact]
    public void GetPlatformsForSDKWithMatchingInstalledTargetPlatforms()
    {
        IEnumerable<string> myPlatforms = ToolLocationHelper.GetPlatformsForSDK("MyPlatform", new Version("8.0"), [_fakeStructureRoot], null);
        myPlatforms.ShouldContain("Sparkle, Version=3.3.3.3");
        myPlatforms.ShouldContain("PlatformAssembly, Version=0.1.2.3");
        myPlatforms.ShouldContain("PlatformAssembly, Version=1.2.3.0");
        myPlatforms.Count().ShouldBe(3);
    }

    /// <summary>
    /// Verify that the list of platforms is empty if we ask for an sdk that is not installed.
    /// </summary>
    [Fact]
    public void GetPlatformsForSDKWithInstalledTargetPlatformsNoMatch()
        => ToolLocationHelper.GetPlatformsForSDK("DoesNotExistPlatform", new Version("0.0.0.0"), [_fakeStructureRoot], null).Any().ShouldBeFalse();

    /// <summary>
    /// Verify that the list of platforms is empty if we ask for a valid sdk identifier but
    /// a version number that isn't installed.
    /// </summary>
    [Fact]
    public void GetPlatformsForSDKWithMatchingPlatformNotMatchingVersion()
        => ToolLocationHelper.GetPlatformsForSDK("MyPlatform", new Version("0.0.0.0"), [_fakeStructureRoot], null).Any().ShouldBeFalse();

    /// <summary>
    /// Verify that if we pass in an sdk identifier and version for an installed legacy platform sdk
    /// that the list of platforms is empty because it has no platforms.
    /// </summary>
    [Fact]
    public void GetPlatformsForSDKForLegacyPlatformSDK()
        => ToolLocationHelper.GetPlatformsForSDK("Windows", new Version("8.0"), [_fakeStructureRoot], null).Any().ShouldBeFalse();

    /// <summary>
    /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we
    /// get the expected set out. Make sure that when we resolve from both the disk and registry that there are no duplicates
    /// and make sure we get the expected results.
    /// </summary>
    [Fact]
    public void GetALLTargetPlatformSDKs()
    {
        try
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
            IList<TargetPlatformSDK> sdks = ToolLocationHelper.GetTargetPlatformSdks([_fakeStructureRoot], null);

            var targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();
            foreach (TargetPlatformSDK sdk in sdks)
            {
                targetPlatforms.Add(sdk, sdk);
            }

            TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
            targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

            key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
            targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

            key = new TargetPlatformSDK("MyPlatform", new Version("3.0"), null);
            targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "3.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

            key = new TargetPlatformSDK("MyPlatform", new Version("2.0"), null);
            targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

            key = new TargetPlatformSDK("MyPlatform", new Version("1.0"), null);
            targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

            key = new TargetPlatformSDK("MyPlatform", new Version("5.0"), null);
            targetPlatforms.ContainsKey(key).ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
        }
    }

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
