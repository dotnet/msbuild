// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    /// <summary>
    /// Make sure that if a unknown framework identifier with a root directory which does not exist in it is passed in then we get an empty list back out.
    /// </summary>
    [Fact]
    public void GetFrameworkIdentifiersNoReferenceAssemblies()
    {
        IList<string> installedIdentifiers =
            ToolLocationHelper.GetFrameworkIdentifiers(
                NativeMethodsShared.IsWindows ? "f:\\IDontExistAtAll" : "/IDontExistAtAll");
        installedIdentifiers.Count.ShouldBe(0);
    }

    /// <summary>
    /// When the root does not exist make sure nothing is returned
    /// </summary>
    [Fact]
    public void HighestVersionOfTargetFrameworkIdentifierRootDoesNotExist()
    {
        FrameworkNameVersioning highestMoniker =
            ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(
                NativeMethodsShared.IsWindows ? "f:\\IDontExistAtAll" : "/IDontExistAtAll",
                ".UnKNownFramework");
        highestMoniker.ShouldBeNull();
    }

    /// <summary>
    /// When the root contains no folders with versions on them make sure nothing is returned
    /// </summary>
    [Fact]
    public void HighestVersionOfTargetFrameworkIdentifierRootNoVersions()
    {
        string tempPath = Path.GetTempPath();
        string testPath = Path.Combine(tempPath, "HighestVersionOfTargetFrameworkIdentifierRootNoVersions");
        string nonVersionFolder = Path.Combine(testPath, ".UnknownFramework", "NotAVersion");

        if (!Directory.Exists(nonVersionFolder))
        {
            Directory.CreateDirectory(nonVersionFolder);
        }

        FrameworkNameVersioning highestMoniker = ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(testPath, ".UnKNownFramework");
        highestMoniker.ShouldBeNull();
    }

    /// <summary>
    /// If a directory contains multiple versions make sure we pick the highest one.
    /// </summary>
    [Fact]
    public void HighestVersionOfTargetFrameworkIdentifierRootMultipleVersions()
    {
        string tempPath = Path.GetTempPath();
        string testPath = Path.Combine(tempPath, "HighestVersionOfTargetFrameworkIdentifierRootMultipleVersions");
        string folder10 = Path.Combine(testPath, ".UnknownFramework", "v1.0");
        string folder20 = Path.Combine(testPath, ".UnknownFramework", "v2.0");
        string folder40 = Path.Combine(testPath, ".UnknownFramework", "v4.0");

        if (!Directory.Exists(folder10))
        {
            Directory.CreateDirectory(folder10);
        }

        if (!Directory.Exists(folder20))
        {
            Directory.CreateDirectory(folder20);
        }

        if (!Directory.Exists(folder40))
        {
            Directory.CreateDirectory(folder40);
        }

        FrameworkNameVersioning highestMoniker =
            ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(testPath, ".UnknownFramework");
        highestMoniker.ShouldNotBeNull();
        highestMoniker.Version.Major.ShouldBe(4);
    }
}
