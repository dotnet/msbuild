// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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
}
