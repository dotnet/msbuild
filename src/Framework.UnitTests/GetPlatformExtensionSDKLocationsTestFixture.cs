// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
#if FEATURE_WIN32_REGISTRY
using Microsoft.Win32;
#endif
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

/// <summary>
/// Verify the toolLocation helper method that enumerates the disk and registry to get the list of installed SDKs.
/// </summary>
public partial class GetPlatformExtensionSDKLocationsTestFixture : IDisposable
{
#if FEATURE_WIN32_REGISTRY
    // Create delegates to mock the registry for the registry portion of the test.
    private readonly OpenBaseKey _openBaseKey = GetBaseKey;
    private readonly GetRegistrySubKeyNames getRegistrySubKeyNames = GetRegistrySubKeyNames;
    private readonly GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue;
#endif

    // Path to the fake SDk directory structure created under the temp directory.
    private readonly string _fakeStructureRoot;
    private readonly string _fakeStructureRoot2;

    public GetPlatformExtensionSDKLocationsTestFixture(ITestOutputHelper output)
    {
#if FEATURE_WIN32_REGISTRY
        getRegistrySubKeyDefaultValue = GetRegistrySubKeyDefaultValue;
#endif

        _fakeStructureRoot = MakeFakeSDKStructure();
        _fakeStructureRoot2 = MakeFakeSDKStructure2();
    }

    public void Dispose()
    {
        if (_fakeStructureRoot != null)
        {
            if (FileUtilities.DirectoryExistsNoThrow(_fakeStructureRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(_fakeStructureRoot, true);
            }
        }

        if (_fakeStructureRoot2 != null)
        {
            if (FileUtilities.DirectoryExistsNoThrow(_fakeStructureRoot2))
            {
                FileUtilities.DeleteDirectoryNoThrow(_fakeStructureRoot2, true);
            }
        }
    }

    /// <summary>
    /// Make a fake SDK structure on disk for testing.
    /// </summary>
    private static string MakeFakeSDKStructure()
    {
        string manifestPlatformSDK1 = """
            <FileList
                DisplayName = "Windows"
                PlatformIdentity = "Windows, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "12.0"
                MinOSVersion = "6.2.1"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "Windows, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        string manifestPlatformSDK2 = """
            <FileList
                DisplayName = "Windows"
                PlatformIdentity = "Windows, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "11.0"
                MinOSVersion = "6.2.2"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "Windows, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        string manifestPlatformSDK3 = """
            <FileList
                DisplayName = "Windows"
                PlatformIdentity = "Windows, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "10.0"
                MinOSVersion = "6.2.3"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "Windows, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        string manifestPlatformSDK4 = """
            <FileList
                DisplayName = "Windows"
                PlatformIdentity = "Windows, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "9.0"
                MinOSVersion = "6.2.4"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "Windows, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        string manifestPlatformSDK5 = """
            <FileList
                DisplayName = "Windows"
                PlatformIdentity = "Windows, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "8.0"
                MinOSVersion = "6.2.5"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "Windows, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        string manifestPlatformSDK6 = """
            <FileList
                DisplayName = "Windows"
                PlatformIdentity = "Windows, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "Windows, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        string manifestPlatformSDK7 = """
            <FileList
                DisplayName = "Windows"
                PlatformIdentity = "Windows, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "8"
                MinOSVersion = "Blah"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "Windows, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        string manifestExtensionSDK1 = """
            <FileList
                DisplayName = "ExtensionSDK2"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "11.0"
                MaxPlatformVersion = "8.0"
                MinOSVersion = "6.2.1"
                MaxOSVersionTested = "6.2.1">

                <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                </File>
            </FileList>
            """;

        string manifestExtensionSDK2 = """
            <FileList
                DisplayName = "ExtensionSDK2"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "11.0"
                MaxPlatformVersion = "9.0"
                MinOSVersion = "6.2.1"
                MaxOSVersionTested = "6.2.1">

                <File Reference = "MySDK.Sprint.winmd" Implementation = "XNASprintImpl.dll">
                </File>
            </FileList>
            """;

        string tempPath = Path.Combine(Path.GetTempPath(), "FakeSDKDirectory");
        try
        {
            // Good
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "SomeOtherPlace", "MyPlatformOtherLocation", "4.0", "ExtensionSDKs", "MyAssembly", "1.0"));
            Directory.CreateDirectory(Path.Combine(tempPath, "WindowsKits", "6.0"));
            Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform", "5.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "4.0", "ExtensionSDKs", "AnotherAssembly", "1.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "3.0", "ExtensionSDKs", "MyAssembly", "1.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "2.0", "ExtensionSDKs", "MyAssembly", "1.0"));
            Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform", "1.0"));
            Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform", "8.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "8.0", "Platforms", "PlatformAssembly", "0.1.2.3"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "8.0", "Platforms", "PlatformAssembly", "1.2.3.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "8.0", "Platforms", "Sparkle", "3.3.3.3"));
            Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform", "9.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "9.0", "Platforms", "PlatformAssembly", "0.1.2.3"));
            Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform", "9.0", "PlatformAssembly", "Sparkle"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "9.0", "Platforms", "PlatformAssembly", "Sparkle"));

            File.WriteAllText(
                Path.Combine(tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0", "SDKManifest.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0", "SDKManifest.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0", "SDKManifest.xml"),
                "Hello");

            File.WriteAllText(
                Path.Combine(tempPath, "SomeOtherPlace", "MyPlatformOtherLocation", "4.0", "SDKManifest.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "SomeOtherPlace", "MyPlatformOtherLocation", "4.0", "ExtensionSDKs", "MyAssembly", "1.0", "SDKManifest.xml"),
                "Hello");
            File.WriteAllText(Path.Combine(tempPath, "Windows", "1.0", "SDKManifest.xml"), manifestPlatformSDK1);
            File.WriteAllText(Path.Combine(tempPath, "Windows", "2.0", "SDKManifest.xml"), manifestPlatformSDK2);
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "4.0", "ExtensionSDKs", "AnotherAssembly", "1.0", "SDKManifest.xml"),
                manifestExtensionSDK2);
            File.WriteAllText(Path.Combine(tempPath, "MyPlatform", "3.0", "SDKManifest.xml"), manifestPlatformSDK3);
            File.WriteAllText(Path.Combine(tempPath, "MyPlatform", "2.0", "SDKManifest.xml"), manifestPlatformSDK4);
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "3.0", "ExtensionSDKs", "MyAssembly", "1.0", "SDKManifest.xml"),
                manifestExtensionSDK1);
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "2.0", "ExtensionSDKs", "MyAssembly", "1.0", "SDKManifest.xml"),
                "Hello");
            File.WriteAllText(Path.Combine(tempPath, "MyPlatform", "1.0", "SDKManifest.xml"), manifestPlatformSDK5);

            // Contains a couple of sub-platforms
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "8.0", "SDKManifest.xml"),
                manifestPlatformSDK6);
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "8.0", "Platforms", "PlatformAssembly", "0.1.2.3", "Platform.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "8.0", "Platforms", "PlatformAssembly", "1.2.3.0", "Platform.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "8.0", "Platforms", "Sparkle", "3.3.3.3", "Platform.xml"),
                "Hello");

            // Contains invalid sub-platforms as well as valid ones
            File.WriteAllText(Path.Combine(tempPath, "MyPlatform", "9.0", "SDKManifest.xml"), manifestPlatformSDK7);
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "9.0", "Platforms", "PlatformAssembly", "0.1.2.3", "Platform.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "9.0", "PlatformAssembly", "Sparkle", "Platform.xml"),
                "Hello"); // not under the Platforms directory
            File.WriteAllText(
                Path.Combine(tempPath, "MyPlatform", "9.0", "Platforms", "PlatformAssembly", "Sparkle", "Platform.xml"),
                "Hello"); // bad version
            Directory.CreateDirectory(
                Path.Combine(tempPath, "MyPlatform", "9.0", "Platforms", "Sparkle", "3.3.3.3")); // no platform.xml

            // Bad because of v in the sdk version
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "v1.0", "ExtensionSDKs", "AnotherAssembly", "v1.1"));

            // Bad because no extensionsdks directory under the platform version
            Directory.CreateDirectory(Path.Combine(tempPath, "Windows", "v3.0") + Path.DirectorySeparatorChar);

            // Bad because the directory under the identifier is not a version
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "NotAVersion") + Path.DirectorySeparatorChar);

            // Bad because the directory under the identifier is not a version
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "NotAVersion", "ExtensionSDKs", "Assembly", "1.0"));
        }
        catch (Exception)
        {
            FileUtilities.DeleteDirectoryNoThrow(tempPath, true);
            return null;
        }

        return tempPath;
    }

    /// <summary>
    /// Make a fake SDK structure on disk for testing.
    /// </summary>
    private static string MakeFakeSDKStructure2()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "FakeSDKDirectory2");
        try
        {
            // Good
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0"));
            Directory.CreateDirectory(
                Path.Combine(tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "4.0"));

            File.WriteAllText(
                Path.Combine(tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0", "SDKManifest.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0", "SDKManifest.xml"),
                "Hello");
            File.WriteAllText(
                Path.Combine(tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "4.0", "SDKManifest.xml"),
                "Hello");
        }
        catch (Exception)
        {
            FileUtilities.DeleteDirectoryNoThrow(tempPath, true);
            return null;
        }

        return tempPath;
    }

#if FEATURE_WIN32_REGISTRY
    /// <summary>
    /// Simplified registry access delegate. Given a baseKey and a subKey, get all of the subkey
    /// names.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The subkey</param>
    /// <returns>An enumeration of strings.</returns>
    private static IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey)
    {
        if (baseKey == Registry.CurrentUser)
        {
            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs", StringComparison.OrdinalIgnoreCase))
            {
                return ["Windows", "MyPlatform"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows", StringComparison.OrdinalIgnoreCase))
            {
                return ["v1.0", "1.0"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase))
            {
                return ["MyAssembly"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\1.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase))
            {
                return ["MyAssembly"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase))
            {
                return ["v1.1", "1.0", "2.0", "3.0"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\1.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase))
            {
                return ["2.0"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform", StringComparison.OrdinalIgnoreCase))
            {
                return ["4.0", "5.0", "6.0", "9.0"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase))
            {
                return ["MyAssembly"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\5.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase))
            {
                return [string.Empty];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase))
            {
                return ["1.0"];
            }
        }

        if (baseKey == Registry.LocalMachine)
        {
            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs", StringComparison.OrdinalIgnoreCase))
            {
                return ["Windows"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows", StringComparison.OrdinalIgnoreCase))
            {
                return ["v2.0"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase))
            {
                return ["MyAssembly"];
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase))
            {
                return ["3.0"];
            }
        }

        return [];
    }

    /// <summary>
    /// Simplified registry access delegate. Given a baseKey and subKey, get the default value
    /// of the subKey.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The subkey</param>
    /// <returns>A string containing the default value.</returns>
    private string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey)
    {
        if (baseKey == Registry.CurrentUser)
        {
            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly\1.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"Windows\v1.0\ExtensionSDKs\MyAssembly\1.0");
            }

            // This has a v in the sdk version and should not be found but we need a real path in case it is so it will show up in the returned list and fail the test.
            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly\v1.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"Windows\v1.0\ExtensionSDKs\MyAssembly\1.0");
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\1.0\ExtensionSDKs\MyAssembly\2.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"Windows\1.0\ExtensionSDKs\MyAssembly\2.0");
            }

            // This has a set of bad char in the returned directory so it should not be allowed.
            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly\3.0", StringComparison.OrdinalIgnoreCase))
            {
                return _fakeStructureRoot + @"\Windows\1.0\ExtensionSDKs\MyAssembly\<>?/";
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\5.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"MyPlatform\5.0");
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"SomeOtherPlace\MyPlatformOtherLocation\4.0");
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\6.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"Windows Kits\6.0");
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\9.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"MyPlatform\9.0");
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0\ExtensionSDKs\MyAssembly\1.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"SomeOtherPlace\MyPlatformOtherLocation\4.0\ExtensionSDKs\MyAssembly\1.0");
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"Windows\1.0");
            }
        }

        if (baseKey == Registry.LocalMachine)
        {
            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0\ExtensionSDKs\MyAssembly\3.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"Windows\2.0\ExtensionSDKs\MyAssembly\3.0");
            }

            if (string.Equals(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_fakeStructureRoot, @"Windows\2.0");
            }
        }

        return null;
    }

    /// <summary>
    /// Registry access delegate. Given a hive and a view, return the registry base key.
    /// </summary>
    private static RegistryKey GetBaseKey(RegistryHive hive, RegistryView view)
        => hive switch
        {
            RegistryHive.CurrentUser => Registry.CurrentUser,
            RegistryHive.LocalMachine => Registry.LocalMachine,
            _ => null,
        };
#endif
}
