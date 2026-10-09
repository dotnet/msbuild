// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
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
    public void GetApiContractReferencesHandlesEmptyContracts()
    {
        string[] returnValue = ToolLocationHelper.GetApiContractReferences(Enumerable.Empty<ApiContract>(), string.Empty);
        returnValue.Length.ShouldBe(0);
    }

    [Fact]
    public void GetApiContractReferencesHandlesNullContracts()
    {
        string[] returnValue = ToolLocationHelper.GetApiContractReferences(null, string.Empty);
        returnValue.Length.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void GetApiContractReferencesHandlesNonExistingLocation()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string[] returnValue = ToolLocationHelper.GetApiContractReferences([new ApiContract { Name = "Foo", Version = "Bar" }], tempDirectory);
        returnValue.Length.ShouldBe(0);
    }

    [WindowsOnlyFact]
    public void GetApiContractReferencesFindsWinMDs()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string referenceDirectory = Path.Combine(tempDirectory, Path.Combine("References", "Foo", "Bar"));

        try
        {
            Directory.CreateDirectory(referenceDirectory);
            File.WriteAllText(Path.Combine(referenceDirectory, "One.winmd"), "First");
            File.WriteAllText(Path.Combine(referenceDirectory, "Two.winmd"), "Second");
            File.WriteAllText(Path.Combine(referenceDirectory, "Three.winmd"), "Third");
            string[] returnValue = ToolLocationHelper.GetApiContractReferences([new ApiContract { Name = "Foo", Version = "Bar" }], tempDirectory);
            returnValue.Length.ShouldBe(3);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(tempDirectory, true);
            }
        }
    }

    [WindowsOnlyFact]
    public void GetApiContractReferencesFindsVersionedWinMDs()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string tempVersion = "10.0.12345.0";
        string referenceDirectory = Path.Combine(tempDirectory, @"References", tempVersion, @"Foo\Bar");

        try
        {
            Directory.CreateDirectory(referenceDirectory);
            File.WriteAllText(Path.Combine(referenceDirectory, "One.winmd"), "First");
            string[] returnValue = ToolLocationHelper.GetApiContractReferences([new ApiContract { Name = "Foo", Version = "Bar" }], tempDirectory, tempVersion);
            returnValue.Length.ShouldBe(1);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }
}
