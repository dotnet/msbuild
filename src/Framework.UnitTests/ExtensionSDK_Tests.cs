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

public class ExtensionSDK_Tests
{
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
                    MaxOSVersionTested = "6.2.1">

                    <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                        <Registration Type = "Flipper" Implementation = "XNASprintFlipperImpl.dll" />
                        <Registration Type = "Flexer" Implementation = "XNASprintFlexerImpl.dll" />
                        <ToolboxItems VSCategory = "Toolbox.Default" />
                    </File>
                </FileList>
                """;

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
}
