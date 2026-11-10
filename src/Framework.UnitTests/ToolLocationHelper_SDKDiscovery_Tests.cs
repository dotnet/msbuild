// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
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
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void GatherExtensionSDKsInvalidVersionDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string sdkDirectory = Path.Combine(tempDirectory, "Foo", "Bar");

        try
        {
            Directory.CreateDirectory(sdkDirectory);
            DirectoryInfo info = new DirectoryInfo(tempDirectory);
            TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(0, 0), string.Empty);
            ToolLocationHelper.GatherExtensionSDKs(info, sdk);
            sdk.ExtensionSDKs.Count.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(tempDirectory, true);
            }
        }
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void GatherExtensionSDKsNoManifest()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string sdkDirectory = Path.Combine(tempDirectory, "Foo", "1.0");

        try
        {
            Directory.CreateDirectory(sdkDirectory);
            DirectoryInfo info = new DirectoryInfo(tempDirectory);
            TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(0, 0), string.Empty);
            ToolLocationHelper.GatherExtensionSDKs(info, sdk);
            sdk.ExtensionSDKs.Count.ShouldBe(0);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(tempDirectory, true);
            }
        }
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void GatherExtensionSDKsEmptyManifest()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string sdkDirectory = Path.Combine(tempDirectory, "Foo", "1.0");

        try
        {
            Directory.CreateDirectory(sdkDirectory);
            File.WriteAllText(Path.Combine(sdkDirectory, "SDKManifest.xml"), "");
            DirectoryInfo info = new DirectoryInfo(tempDirectory);
            TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(0, 0), string.Empty);
            ToolLocationHelper.GatherExtensionSDKs(info, sdk);
            sdk.ExtensionSDKs.Count.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(tempDirectory, true);
            }
        }
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void GatherExtensionSDKsGarbageManifest()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string sdkDirectory = Path.Combine(tempDirectory, "Foo", "1.0");

        try
        {
            Directory.CreateDirectory(sdkDirectory);
            File.WriteAllText(Path.Combine(sdkDirectory, "SDKManifest.xml"), "Garbaggggge");
            DirectoryInfo info = new DirectoryInfo(tempDirectory);
            TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(0, 0), string.Empty);
            ToolLocationHelper.GatherExtensionSDKs(info, sdk);
            sdk.ExtensionSDKs.Count.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(tempDirectory, true);
            }
        }
    }
}
