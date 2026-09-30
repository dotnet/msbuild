// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    [WindowsOnlyFact]
    public void GetWinBlueSDKLocation()
    {
        string sdkRootPath = ToolLocationHelper.GetPlatformSDKLocation("Windows", "8.1");

        string returnValue = ToolLocationHelper.GetSDKContentFolderPath("Windows", "8.1", null, null, null, null);
        returnValue.ShouldBe(sdkRootPath);
    }

    [WindowsOnlyFact]
    public void GetWinBlueContentFolderPath()
    {
        string sdkRootPath = ToolLocationHelper.GetPlatformSDKLocation("Windows", "8.1");

        string returnValue = ToolLocationHelper.GetSDKContentFolderPath("Windows", "8.1", null, null, null, @"DesignTime\CommonConfiguration\Neutral");
        returnValue.ShouldBe(Path.Combine(sdkRootPath, @"DesignTime\CommonConfiguration\Neutral"));
    }

    [WindowsOnlyFact]
    public void GetSDKRootLocation()
    {
        string expectedValue = ToolLocationHelper.GetPlatformSDKLocation("Windows", "10.0");

        string versionedSDKValue = ToolLocationHelper.GetSDKContentFolderPath("Windows", "10.0", "UAP", "10.0.14944.0", "10.0.14944.0", null);
        versionedSDKValue.ShouldBe(expectedValue);

        string unversionedSDKValue = ToolLocationHelper.GetSDKContentFolderPath("Windows", "10.0", "UAP", "10.0.10586.0", "10.0.10586.0", null);
        unversionedSDKValue.ShouldBe(expectedValue);
    }

#if RUNTIME_TYPE_NETCORE
    [WindowsOnlyFact(Skip = "https://github.com/dotnet/msbuild/issues/1250")]
#else
    [WindowsOnlyFact(Skip = "https://github.com/dotnet/msbuild/issues/2569")]
#endif
    public void GetUnversionedSDKUnionMetadataLocation()
    {
        string sdkRootPath = ToolLocationHelper.GetPlatformSDKLocation("Windows", "10.0");
        string returnValue = ToolLocationHelper.GetSDKContentFolderPath("Windows", "10.0", "UAP", "10.0.14393.0", "10.0.14393.0", "UnionMetadata");

        returnValue.ShouldNotContain("10.0.14393.0");
        returnValue.ShouldBe(Path.Combine(sdkRootPath, "UnionMetadata"));
    }

    [WindowsOnlyFact]
    public void GetVersionedSDKUnionMetadataLocation()
    {
        // Create manifest file
        string platformRootFolder = Path.Combine(Path.GetTempPath(), @"MockSDK");
        string sdkRootFolder = Path.Combine(platformRootFolder, @"Windows Kits\10");
        string platformFolder = Path.Combine(sdkRootFolder, @"Platforms\UAP\10.0.14944.0");
        string platformFilePath = Path.Combine(platformFolder, "Platform.xml");
        string sdkManifestFilePath = Path.Combine(sdkRootFolder, "SDKManifest.xml");

        bool useTempPlatformFile = false;
        try
        {
            if (!File.Exists(platformFilePath))
            {
                if (!Directory.Exists(sdkRootFolder))
                {
                    Directory.CreateDirectory(sdkRootFolder);
                }

                if (!Directory.Exists(platformFolder))
                {
                    Directory.CreateDirectory(platformFolder);
                }

                string sdkManifestFileContent = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<FileList
  TargetPlatform=""UAP""
  TargetPlatformMinVersion=""10.0.0.0""
  TargetPlatformVersion=""10.0.14944.0""
  DisplayName = ""Microsoft Mock SDK for UAP 10.0.14944.0""
  AppliesTo = ""WindowsAppContainer + (Managed | Javascript | Native)""
  MinVSVersion = ""14.0""
  SupportsMultipleVersions=""Error""
  SupportedArchitectures=""x86;x64;ARM;ARM64"">
</FileList>";
                string platformFileContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ApplicationPlatform name=""UAP"" friendlyName=""Windows 10 Anniversary Edition Insider Preview"" version=""10.0.14944.0"">
   <VersionedContent>true</VersionedContent>
</ApplicationPlatform>";

                File.WriteAllText(platformFilePath, platformFileContent);
                File.WriteAllText(sdkManifestFilePath, sdkManifestFileContent);

                useTempPlatformFile = true;
            }

            // Get and verify return value
            string returnValue = ToolLocationHelper.GetSDKContentFolderPath("Windows", "10.0", "UAP", "10.0.14944.0", "10.0.14944.0", "UnionMetadata", platformRootFolder);
            returnValue.ShouldBe(Path.Combine(sdkRootFolder, "UnionMetadata", "10.0.14944.0"));
        }
        finally
        {
            if (useTempPlatformFile)
            {
                FileUtilities.DeleteDirectoryNoThrow(platformRootFolder, true);
            }
        }
    }
}
