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
