// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

public partial class GetPlatformExtensionSDKLocationsTestFixture
{

    /// <summary>
    /// Verify we do not get any resolved paths when we pass in a root which is too long
    ///
    /// </summary>
    [WindowsFullFrameworkOnlyFact]
    public void ResolveFromDirectoryPathTooLong()
        => Should.Throw<PathTooLongException>(() =>
        {
            // Try a path too long, which does not exist
            string tooLongPath = NativeMethodsShared.IsWindows
                                ? (@"C:\" + new string('g', 1800))
                                : ("/" + new string('g', 10000));

            var paths = new List<string> { tooLongPath };
            var targetPlatform = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

            ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatform);
        });

    /// <summary>
    /// Verify we get no resolved paths when we pass in a root with invalid chars
    /// </summary>
    [WindowsFullFrameworkOnlyFact(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486. No invalid characters on Unix.")]
    public void ResolveFromDirectoryInvalidChar()
    {
        var targetPlatform = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

        // Try a path with invalid chars which does not exist
        string directoryWithInvalidChars = "c:\\<>?";
        var paths = new List<string> { directoryWithInvalidChars };
        Should.Throw<ArgumentException>(() => { ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatform); });
    }

    /// <summary>
    /// Verify we get no resolved paths when we pass in a path which does not exist.
    ///
    /// </summary>
    [Fact]
    public void ResolveFromDirectoryNotExist()
    {
        var targetPlatform = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

        // Try a regular path which does not exist.
        string normalDirectory = NativeMethodsShared.IsWindows ? "c:\\SDKPath" : "/SDKPath";
        var paths = new List<string> { normalDirectory };
        ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatform);
        targetPlatform.Count.ShouldBe(0);
    }

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
            string manifestExtensionSDK = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxPlatformVersion = ""9.0""
                    MinOSVersion = ""6.2.3""
                    MaxOSVersionTested = ""6.2.2"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

            File.WriteAllText(manifestFile, manifestExtensionSDK);
            SDKManifest sdkManifest = new SDKManifest(frameworkPath);

            sdkManifest.FrameworkIdentities.ShouldNotBeNull();
            sdkManifest.FrameworkIdentities.Count.ShouldBeGreaterThan(0);
            sdkManifest.MaxPlatformVersion.ShouldBe("9.0");
            sdkManifest.MinOSVersion.ShouldBe("6.2.3");
            sdkManifest.MaxOSVersionTested.ShouldBe("6.2.2");

            // This is a framework SDK and the values default b/c they are not in the manifest
            string manifestExtensionSDK2 = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK"">


                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

            File.WriteAllText(manifestFile, manifestExtensionSDK2);
            SDKManifest sdkManifest2 = new SDKManifest(frameworkPath);

            sdkManifest2.FrameworkIdentities.ShouldNotBeNull();
            sdkManifest2.FrameworkIdentities.Count.ShouldBeGreaterThan(0);
            sdkManifest2.MaxPlatformVersion.ShouldBe("8.0");
            sdkManifest2.MinOSVersion.ShouldBe("6.2.1");
            sdkManifest2.MaxOSVersionTested.ShouldBe("6.2.1");

            // This is not a framework SDK because it does not have FrameworkIdentity set
            string manifestExtensionSDK3 = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK"">


                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

            File.WriteAllText(manifestFile, manifestExtensionSDK3);
            SDKManifest sdkManifest3 = new SDKManifest(frameworkPath);

            sdkManifest3.FrameworkIdentity.ShouldBeNull();
            sdkManifest3.MaxPlatformVersion.ShouldBeNull();
            sdkManifest3.MinOSVersion.ShouldBeNull();
            sdkManifest3.MaxOSVersionTested.ShouldBeNull();

            // This is not a framework SDK because of its location
            string manifestExtensionSDK4 = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""


                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

            File.WriteAllText(manifestFile2, manifestExtensionSDK4);
            SDKManifest sdkManifest4 = new SDKManifest(frameworkPath2);

            sdkManifest4.FrameworkIdentity.ShouldBeNull();
            sdkManifest4.MaxPlatformVersion.ShouldBeNull();
            sdkManifest4.MinOSVersion.ShouldBeNull();
            sdkManifest4.MaxOSVersionTested.ShouldBeNull();

            // This is a framework SDK with partially specified values, some default values are used
            string manifestExtensionSDK5 = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxOSVersionTested = ""6.2.2"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

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

            string manifestPlatformSDK = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
                    <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

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

            string manifestExtensionSDK = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity-Debug = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    FrameworkIdentity-Retail = ""Name=MySDK.10, MinVersion=1.0.0.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    AppliesTo = ""WindowsAppContainer + WindowsXAML""
                    SupportPrefer32Bit = ""True""
                    SupportedArchitectures = ""x86;x64;ARM""
                    SupportsMultipleVersions = ""Error""
                    AppX-Debug-x86 = "".\AppX\Debug\x86\Microsoft.MySDK.x86.Debug.1.0.appx""
                    AppX-Debug-x64 = "".\AppX\Debug\x64\Microsoft.MySDK.x64.Debug.1.0.appx""
                    AppX-Debug-ARM = "".\AppX\Debug\ARM\Microsoft.MySDK.ARM.Debug.1.0.appx""
                    AppX-Retail-x86 = "".\AppX\Retail\x86\Microsoft.MySDK.x86.1.0.appx""
                    AppX-Retail-x64 = "".\AppX\Retail\x64\Microsoft.MySDK.x64.1.0.appx""
                    AppX-Retail-ARM = "".\AppX\Retail\ARM\Microsoft.MySDK.ARM.1.0.appx""
                    CopyRedistToSubDirectory = "".""
                    DependsOn = ""SDKB, version=2.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxPlatformVersion = ""8.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.3"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

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

    /// <summary>
    /// Verify ExtensionSDK
    /// </summary>
    [Fact]
    public void VerifyExtensionSDK()
    {
        string manifestPath = Path.Combine(Path.GetTempPath(), "ManifestTmp");

        try
        {
            Directory.CreateDirectory(manifestPath);

            string manifestFile = Path.Combine(manifestPath, "SDKManifest.xml");

            string manifestExtensionSDK = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity-Debug = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    FrameworkIdentity-Retail = ""Name=MySDK.10, MinVersion=1.0.0.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    AppliesTo = ""WindowsAppContainer + WindowsXAML""
                    SupportPrefer32Bit = ""True""
                    SupportedArchitectures = ""x86;x64;ARM""
                    SupportsMultipleVersions = ""Error""
                    AppX-Debug-x86 = "".\AppX\Debug\x86\Microsoft.MySDK.x86.Debug.1.0.appx""
                    AppX-Debug-x64 = "".\AppX\Debug\x64\Microsoft.MySDK.x64.Debug.1.0.appx""
                    AppX-Debug-ARM = "".\AppX\Debug\ARM\Microsoft.MySDK.ARM.Debug.1.0.appx""
                    AppX-Retail-x86 = "".\AppX\Retail\x86\Microsoft.MySDK.x86.1.0.appx""
                    AppX-Retail-x64 = "".\AppX\Retail\x64\Microsoft.MySDK.x64.1.0.appx""
                    AppX-Retail-ARM = "".\AppX\Retail\ARM\Microsoft.MySDK.ARM.1.0.appx""
                    CopyRedistToSubDirectory = "".""
                    DependsOn = ""SDKB, version=2.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxPlatformVersion = ""8.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

            File.WriteAllText(manifestFile, manifestExtensionSDK);
            ExtensionSDK extensionSDK = new ExtensionSDK(
                $"CppUnitTestFramework, Version={ObjectModelHelpers.CurrentVisualStudioVersion}", manifestPath);

            extensionSDK.Identifier.ShouldBe("CppUnitTestFramework");
            extensionSDK.MaxPlatformVersion.ShouldBe(new Version("8.0"));
            extensionSDK.MinVSVersion.ShouldBe(new Version("11.0"));
            extensionSDK.Version.ShouldBe(new Version(ObjectModelHelpers.CurrentVisualStudioVersion));
        }
        finally
        {
            FileUtilities.DeleteWithoutTrailingBackslash(manifestPath, true /* for recursive deletion */);
        }
    }

    /// <summary>
    /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we
    /// get the expected set out.
    /// </summary>
    [Fact]
    public void ResolveSDKFromDirectory()
    {
        var paths = new List<string> { _fakeStructureRoot, _fakeStructureRoot2 };
        var targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

        ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatforms);

        TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(2);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=1.0");
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=2.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=2.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(2);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=3.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=3.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=4.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=4.0"].ShouldBe(Path.Combine(_fakeStructureRoot2, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "4.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        // Windows kits special case is only in registry
        key = new TargetPlatformSDK("MyPlatform", new Version("6.0"), null);
        targetPlatforms.ContainsKey(key).ShouldBeFalse();

        key = new TargetPlatformSDK("MyPlatform", new Version("4.0"), null);
        targetPlatforms[key].Path.ShouldBeNull();
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("AnotherAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["AnotherAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "4.0", "ExtensionSDKs", "AnotherAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("3.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "3.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "3.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("2.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "2.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("1.0"), null);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(0);

        key = new TargetPlatformSDK("MyPlatform", new Version("8.0"), null);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "8.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(0);
        targetPlatforms[key].Platforms.Count.ShouldBe(3);
        targetPlatforms[key].ContainsPlatform("PlatformAssembly", "0.1.2.3").ShouldBeTrue();
        targetPlatforms[key].Platforms["PlatformAssembly, Version=0.1.2.3"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "8.0", "Platforms", "PlatformAssembly", "0.1.2.3") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ContainsPlatform("PlatformAssembly", "1.2.3.0").ShouldBeTrue();
        targetPlatforms[key].ContainsPlatform("Sparkle", "3.3.3.3").ShouldBeTrue();

        key = new TargetPlatformSDK("MyPlatform", new Version("9.0"), null);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "9.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(0);
        targetPlatforms[key].Platforms.Count.ShouldBe(1);
        targetPlatforms[key].ContainsPlatform("PlatformAssembly", "0.1.2.3").ShouldBeTrue();
        targetPlatforms[key].Platforms["PlatformAssembly, Version=0.1.2.3"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "9.0", "Platforms", "PlatformAssembly", "0.1.2.3") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
    }

#if FEATURE_REGISTRY_SDKS
    /// <summary>
    /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we
    /// get the expected set out.
    /// </summary>
    [WindowsOnlyFact("No registry unless under Windows.")]
    public void ResolveSDKFromRegistry()
    {
        var targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

        ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.CurrentUser, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, File.Exists);
        ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.LocalMachine, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, File.Exists);

        TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(2);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=2.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=2.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=3.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=3.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("5.0"), null);
        targetPlatforms.ShouldContainKey(key);
        targetPlatforms[key].Path.ShouldBeNull();

        key = new TargetPlatformSDK("MyPlatform", new Version("6.0"), null);
        targetPlatforms.ShouldContainKey(key);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows Kits", "6.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("4.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "SomeOtherPlace", "MyPlatformOtherLocation", "4.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldContainKey("MyAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "SomeOtherPlace", "MyPlatformOtherLocation", "4.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("9.0"), null);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "9.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(0);
        targetPlatforms[key].Platforms.Count.ShouldBe(1);
        targetPlatforms[key].ContainsPlatform("PlatformAssembly", "0.1.2.3").ShouldBeTrue();
        targetPlatforms[key].Platforms["PlatformAssembly, Version=0.1.2.3"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "9.0", "Platforms", "PlatformAssembly", "0.1.2.3") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we
    /// get the expected set out. Make sure that when we resolve from both the disk and registry that there are no duplicates
    /// and make sure we get the expected results.
    /// </summary>
    [Fact]
    public void ResolveSDKFromRegistryAndDisk()
    {
        var targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

        var paths = new List<string> { _fakeStructureRoot };

        ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatforms);

        if (NativeMethodsShared.IsWindows)
        {
            ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.CurrentUser, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, File.Exists);
            ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.LocalMachine, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, File.Exists);
        }

        TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(2);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Keys.ShouldContain("MyAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Keys.ShouldContain("MyAssembly, Version=2.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=2.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Keys.ShouldContain("MyAssembly, Version=3.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=3.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        // This is present in the registry but not on disk.
        key = new TargetPlatformSDK("MyPlatform", new Version("6.0"), null);
        targetPlatforms.Keys.ShouldContain(key);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "Windows Kits", "6.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("5.0"), null);
        targetPlatforms.Keys.ShouldContain(key);
        targetPlatforms[key].ExtensionSDKs.ShouldBeEmpty();
        targetPlatforms[key].Path.ShouldBeNull();

        key = new TargetPlatformSDK("MyPlatform", new Version("4.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(2);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "SomeOtherPlace", "MyPlatformOtherLocation", "4.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Keys.ShouldContain("MyAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "SomeOtherPlace", "MyPlatformOtherLocation", "4.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.Keys.ShouldContain("AnotherAssembly, Version=1.0");
        targetPlatforms[key].ExtensionSDKs["AnotherAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "4.0", "ExtensionSDKs", "AnotherAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("3.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].ExtensionSDKs.Keys.ShouldContain("MyAssembly, Version=1.0");
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "3.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "3.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("2.0"), null);
        targetPlatforms[key].ExtensionSDKs.Count.ShouldBe(1);
        targetPlatforms[key].ExtensionSDKs.Keys.ShouldContain("MyAssembly, Version=1.0");
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "2.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "2.0", "ExtensionSDKs", "MyAssembly", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);

        key = new TargetPlatformSDK("MyPlatform", new Version("1.0"), null);
        targetPlatforms[key].Path.ShouldBe(Path.Combine(_fakeStructureRoot, "MyPlatform", "1.0") + Path.DirectorySeparatorChar, StringCompareShould.IgnoreCase);
        targetPlatforms[key].ExtensionSDKs.ShouldBeEmpty();
    }
#endif
}
