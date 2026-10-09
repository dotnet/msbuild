// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

public class SDKManifest_Tests
{
    [Fact]
    public void VerifySDKManifestWithNullOrEmptyParameter()
    {
        Should.Throw<ArgumentNullException>(() => new SDKManifest(null));
        Should.Throw<ArgumentException>(() => new SDKManifest(""));
    }

    /// <summary>
    /// Verify SDKManifest defaults values for MaxPlatformVersion, MinOSVersion, MaxOSVersion when these are not
    /// present in the manifest and the SDK is a framework extension SDK
    /// </summary>
    [WindowsOnlyFact]
    public void VerifyFrameworkSdkWithOldManifest()
    {
        string tmpRootDirectory = Path.GetTempPath();
        string frameworkPathPattern = NativeMethodsShared.IsWindows ? @"Microsoft SDKs\Windows\v8.0\ExtensionSDKs\MyFramework" : "Microsoft SDKs/Windows/v8.0/ExtensionSDKs/MyFramework";
        string frameworkPathPattern2 = NativeMethodsShared.IsWindows ? @"ExtensionSDKs\MyFramework" : "ExtensionSDKs/MyFramework";

        string frameworkPath = Path.Combine(tmpRootDirectory, frameworkPathPattern);
        string manifestFile = Path.Combine(frameworkPath, "SDKManifest.xml");

        string frameworkPath2 = Path.Combine(tmpRootDirectory, frameworkPathPattern2);
        string manifestFile2 = Path.Combine(frameworkPath, "SDKManifest.xml");

        try
        {
            Directory.CreateDirectory(frameworkPath);
            Directory.CreateDirectory(frameworkPath2);

            // This is a framework SDK with specified values, no default ones are used
            string manifestExtensionSDK = """
                <FileList
                    DisplayName = "My SDK"
                    ProductFamilyName = "UnitTest SDKs"
                    FrameworkIdentity = "Name=MySDK.10.Debug, MinVersion=1.0.0.0"
                    MoreInfo = "http://msdn.microsoft.com/MySDK"
                    MaxPlatformVersion = "9.0"
                    MinOSVersion = "6.2.3"
                    MaxOSVersionTested = "6.2.2">

                    <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                        <Registration Type = "Flipper" Implementation = "XNASprintFlipperImpl.dll" />
                        <Registration Type = "Flexer" Implementation = "XNASprintFlexerImpl.dll" />
                        <ToolboxItems VSCategory = "Toolbox.Default" />
                    </File>
                </FileList>
                """;

            File.WriteAllText(manifestFile, manifestExtensionSDK);
            SDKManifest sdkManifest = new SDKManifest(frameworkPath);

            sdkManifest.FrameworkIdentities.ShouldNotBeNull();
            sdkManifest.FrameworkIdentities.Count.ShouldBeGreaterThan(0);
            sdkManifest.MaxPlatformVersion.ShouldBe("9.0");
            sdkManifest.MinOSVersion.ShouldBe("6.2.3");
            sdkManifest.MaxOSVersionTested.ShouldBe("6.2.2");

            // This is a framework SDK and the values default b/c they are not in the manifest
            string manifestExtensionSDK2 = """
                <FileList
                    DisplayName = "My SDK"
                    ProductFamilyName = "UnitTest SDKs"
                    FrameworkIdentity = "Name=MySDK.10.Debug, MinVersion=1.0.0.0"
                    MoreInfo = "http://msdn.microsoft.com/MySDK">


                    <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                        <Registration Type = "Flipper" Implementation = "XNASprintFlipperImpl.dll" />
                        <Registration Type = "Flexer" Implementation = "XNASprintFlexerImpl.dll" />
                        <ToolboxItems VSCategory = "Toolbox.Default" />
                    </File>
                </FileList>
                """;

            File.WriteAllText(manifestFile, manifestExtensionSDK2);
            SDKManifest sdkManifest2 = new SDKManifest(frameworkPath);

            sdkManifest2.FrameworkIdentities.ShouldNotBeNull();
            sdkManifest2.FrameworkIdentities.Count.ShouldBeGreaterThan(0);
            sdkManifest2.MaxPlatformVersion.ShouldBe("8.0");
            sdkManifest2.MinOSVersion.ShouldBe("6.2.1");
            sdkManifest2.MaxOSVersionTested.ShouldBe("6.2.1");

            // This is not a framework SDK because it does not have FrameworkIdentity set
            string manifestExtensionSDK3 = """
                <FileList
                    DisplayName = "My SDK"
                    ProductFamilyName = "UnitTest SDKs"
                    MoreInfo = "http://msdn.microsoft.com/MySDK">


                    <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                        <Registration Type = "Flipper" Implementation = "XNASprintFlipperImpl.dll" />
                        <Registration Type = "Flexer" Implementation = "XNASprintFlexerImpl.dll" />
                        <ToolboxItems VSCategory = "Toolbox.Default" />
                    </File>
                </FileList>
                """;

            File.WriteAllText(manifestFile, manifestExtensionSDK3);
            SDKManifest sdkManifest3 = new SDKManifest(frameworkPath);

            sdkManifest3.FrameworkIdentity.ShouldBeNull();
            sdkManifest3.MaxPlatformVersion.ShouldBeNull();
            sdkManifest3.MinOSVersion.ShouldBeNull();
            sdkManifest3.MaxOSVersionTested.ShouldBeNull();

            // This is not a framework SDK because of its location
            string manifestExtensionSDK4 = """
                <FileList
                    DisplayName = "My SDK"
                    ProductFamilyName = "UnitTest SDKs"
                    FrameworkIdentity = "Name=MySDK.10.Debug, MinVersion=1.0.0.0"
                    MoreInfo = "http://msdn.microsoft.com/MySDK"


                    <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                        <Registration Type = "Flipper" Implementation = "XNASprintFlipperImpl.dll" />
                        <Registration Type = "Flexer" Implementation = "XNASprintFlexerImpl.dll" />
                        <ToolboxItems VSCategory = "Toolbox.Default" />
                    </File>
                </FileList>
                """;

            File.WriteAllText(manifestFile2, manifestExtensionSDK4);
            SDKManifest sdkManifest4 = new SDKManifest(frameworkPath2);

            sdkManifest4.FrameworkIdentity.ShouldBeNull();
            sdkManifest4.MaxPlatformVersion.ShouldBeNull();
            sdkManifest4.MinOSVersion.ShouldBeNull();
            sdkManifest4.MaxOSVersionTested.ShouldBeNull();

            // This is a framework SDK with partially specified values, some default values are used
            string manifestExtensionSDK5 = """
                <FileList
                    DisplayName = "My SDK"
                    ProductFamilyName = "UnitTest SDKs"
                    FrameworkIdentity = "Name=MySDK.10.Debug, MinVersion=1.0.0.0"
                    MoreInfo = "http://msdn.microsoft.com/MySDK"
                    MaxOSVersionTested = "6.2.2">

                    <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                        <Registration Type = "Flipper" Implementation = "XNASprintFlipperImpl.dll" />
                        <Registration Type = "Flexer" Implementation = "XNASprintFlexerImpl.dll" />
                        <ToolboxItems VSCategory = "Toolbox.Default" />
                    </File>
                </FileList>
                """;

            File.WriteAllText(manifestFile, manifestExtensionSDK5);
            SDKManifest sdkManifest5 = new SDKManifest(frameworkPath);

            sdkManifest5.FrameworkIdentities.ShouldNotBeNull();
            sdkManifest5.FrameworkIdentities.Count.ShouldBeGreaterThan(0);
            sdkManifest5.MaxPlatformVersion.ShouldBe("8.0");
            sdkManifest5.MinOSVersion.ShouldBe("6.2.1");
            sdkManifest5.MaxOSVersionTested.ShouldBe("6.2.2");
        }
        finally
        {
            FileUtilities.DeleteWithoutTrailingBackslash(frameworkPath, true /* for recursive deletion */);
            FileUtilities.DeleteWithoutTrailingBackslash(frameworkPath2, true /* for recursive deletion */);
        }
    }

    /// <summary>
    /// Verify that SDKManifest properties map correctly to properties in SDKManifest.xml.
    /// </summary>
    [Fact]
    public void VerifySDKManifest()
    {
        string manifestPath = Path.Combine(Path.GetTempPath(), "ManifestTmp");

        try
        {
            Directory.CreateDirectory(manifestPath);

            string manifestFile = Path.Combine(manifestPath, "SDKManifest.xml");

            string manifestPlatformSDK = """
                <FileList
                    DisplayName = "Windows"
                    PlatformIdentity = "Windows, version=8.0"
                    TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                    MinVSVersion = "11.0"
                    MinOSVersion = "6.2.1"
                    MaxOSVersionTested = "6.2.1"
                    UnsupportedDowntarget = "Windows, version=8.0">

                    <File Reference = "Windows">
                        <ToolboxItems VSCategory = "Toolbox.Default"/>
                    </File>
                </FileList>
                """;

            File.WriteAllText(manifestFile, manifestPlatformSDK);
            SDKManifest sdkManifest = new SDKManifest(manifestPath);

            sdkManifest.AppxLocations.ShouldBeNull();
            sdkManifest.CopyRedistToSubDirectory.ShouldBeNull();
            sdkManifest.DependsOnSDK.ShouldBeNull();
            sdkManifest.DisplayName.ShouldBe("Windows");
            sdkManifest.FrameworkIdentities.ShouldBeNull();
            sdkManifest.FrameworkIdentity.ShouldBeNull();
            sdkManifest.MaxPlatformVersion.ShouldBeNull();
            sdkManifest.MinVSVersion.ShouldBe("11.0");
            sdkManifest.MinOSVersion.ShouldBe("6.2.1");
            sdkManifest.PlatformIdentity.ShouldBe("Windows, version=8.0");
            sdkManifest.ProductFamilyName.ShouldBeNull();
            sdkManifest.SDKType.ShouldBe(SDKType.Unspecified);
            sdkManifest.SupportedArchitectures.ShouldBeNull();
            sdkManifest.SupportPrefer32Bit.ShouldBeNull();
            sdkManifest.SupportsMultipleVersions.ShouldBe(MultipleVersionSupport.Allow);
            sdkManifest.ReadError.ShouldBeFalse();

            string manifestExtensionSDK = """
                <FileList
                    DisplayName = "My SDK"
                    ProductFamilyName = "UnitTest SDKs"
                    FrameworkIdentity-Debug = "Name=MySDK.10.Debug, MinVersion=1.0.0.0"
                    FrameworkIdentity-Retail = "Name=MySDK.10, MinVersion=1.0.0.0"
                    TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                    MinVSVersion = "11.0"
                    AppliesTo = "WindowsAppContainer + WindowsXAML"
                    SupportPrefer32Bit = "True"
                    SupportedArchitectures = "x86;x64;ARM"
                    SupportsMultipleVersions = "Error"
                    AppX-Debug-x86 = ".\AppX\Debug\x86\Microsoft.MySDK.x86.Debug.1.0.appx"
                    AppX-Debug-x64 = ".\AppX\Debug\x64\Microsoft.MySDK.x64.Debug.1.0.appx"
                    AppX-Debug-ARM = ".\AppX\Debug\ARM\Microsoft.MySDK.ARM.Debug.1.0.appx"
                    AppX-Retail-x86 = ".\AppX\Retail\x86\Microsoft.MySDK.x86.1.0.appx"
                    AppX-Retail-x64 = ".\AppX\Retail\x64\Microsoft.MySDK.x64.1.0.appx"
                    AppX-Retail-ARM = ".\AppX\Retail\ARM\Microsoft.MySDK.ARM.1.0.appx"
                    CopyRedistToSubDirectory = "."
                    DependsOn = "SDKB, version=2.0"
                    MoreInfo = "http://msdn.microsoft.com/MySDK"
                    MaxPlatformVersion = "8.0"
                    MinOSVersion = "6.2.1"
                    MaxOSVersionTested = "6.2.3">

                    <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                        <Registration Type = "Flipper" Implementation = "XNASprintFlipperImpl.dll" />
                        <Registration Type = "Flexer" Implementation = "XNASprintFlexerImpl.dll" />
                        <ToolboxItems VSCategory = "Toolbox.Default" />
                    </File>
                </FileList>
                """;

            File.WriteAllText(manifestFile, manifestExtensionSDK);
            sdkManifest = new SDKManifest(manifestPath);

            sdkManifest.AppxLocations.ShouldContainKey("AppX-Debug-x86");
            sdkManifest.AppxLocations.ShouldContainKey("AppX-Debug-x64");
            sdkManifest.AppxLocations.ShouldContainKey("AppX-Debug-ARM");

            sdkManifest.AppxLocations.ShouldContainKey("AppX-Retail-x86");
            sdkManifest.AppxLocations.ShouldContainKey("AppX-Retail-x64");
            sdkManifest.AppxLocations.ShouldContainKey("AppX-Retail-ARM");

            sdkManifest.AppxLocations["AppX-Debug-x86"].ShouldBe(".\\AppX\\Debug\\x86\\Microsoft.MySDK.x86.Debug.1.0.appx");
            sdkManifest.AppxLocations["AppX-Debug-x64"].ShouldBe(".\\AppX\\Debug\\x64\\Microsoft.MySDK.x64.Debug.1.0.appx");
            sdkManifest.AppxLocations["AppX-Debug-ARM"].ShouldBe(".\\AppX\\Debug\\ARM\\Microsoft.MySDK.ARM.Debug.1.0.appx");

            sdkManifest.AppxLocations["AppX-Retail-x86"].ShouldBe(".\\AppX\\Retail\\x86\\Microsoft.MySDK.x86.1.0.appx");
            sdkManifest.AppxLocations["AppX-Retail-x64"].ShouldBe(".\\AppX\\Retail\\x64\\Microsoft.MySDK.x64.1.0.appx");
            sdkManifest.AppxLocations["AppX-Retail-ARM"].ShouldBe(".\\AppX\\Retail\\ARM\\Microsoft.MySDK.ARM.1.0.appx");

            sdkManifest.CopyRedistToSubDirectory.ShouldBe(".");
            sdkManifest.DependsOnSDK.ShouldBe("SDKB, version=2.0");
            sdkManifest.DisplayName.ShouldBe("My SDK");

            sdkManifest.FrameworkIdentities.ShouldContainKey("FrameworkIdentity-Debug");
            sdkManifest.FrameworkIdentities.ShouldContainKey("FrameworkIdentity-Retail");

            sdkManifest.FrameworkIdentities["FrameworkIdentity-Debug"].ShouldBe("Name=MySDK.10.Debug, MinVersion=1.0.0.0");
            sdkManifest.FrameworkIdentities["FrameworkIdentity-Retail"].ShouldBe("Name=MySDK.10, MinVersion=1.0.0.0");

            sdkManifest.FrameworkIdentity.ShouldBeNull();
            sdkManifest.MaxPlatformVersion.ShouldBe("8.0");
            sdkManifest.MinVSVersion.ShouldBe("11.0");
            sdkManifest.MinOSVersion.ShouldBe("6.2.1");
            sdkManifest.MaxOSVersionTested.ShouldBe("6.2.3");
            sdkManifest.PlatformIdentity.ShouldBeNull();
            sdkManifest.ProductFamilyName.ShouldBe("UnitTest SDKs");
            sdkManifest.SDKType.ShouldBe(SDKType.Unspecified);
            sdkManifest.SupportedArchitectures.ShouldBe("x86;x64;ARM");
            sdkManifest.SupportPrefer32Bit.ShouldBe("True");
            sdkManifest.SupportsMultipleVersions.ShouldBe(MultipleVersionSupport.Error);
            sdkManifest.MoreInfo.ShouldBe("http://msdn.microsoft.com/MySDK");
            sdkManifest.ReadError.ShouldBeFalse();

            File.WriteAllText(manifestFile, "Hello");
            sdkManifest = new SDKManifest(manifestPath);

            sdkManifest.ReadError.ShouldBeTrue();
        }
        finally
        {
            FileUtilities.DeleteWithoutTrailingBackslash(manifestPath, true /* for recursive deletion */);
        }
    }
}
