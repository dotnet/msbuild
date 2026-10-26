// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests;

#if FEATURE_WIN32_REGISTRY
public class AssemblyFoldersEx_Tests
{
    private static List<string> s_assemblyFolderExTestVersions = new List<string>
    {
        "v1.0",
        "v2.0.50727",
        "v3.0",
        "v3.5",
        "v4.0",
        "v4.0.2116",
        "v4.1",
        "v4.0.255",
        "v4.0.255.87",
        "v4.0.9999",
        "v4.0.0000",
        "v4.0001.0",
        "v4.0.2116.87",
        "v3.0SP1",
        "v3.0 BAZ",
        "v5.0",
        "v1",
        "v5",
        "v3.5.0.x86chk",
        "v3.5.1.x86chk",
        "v3.5.256.x86chk",
        "v",
        "1",
        "1.0",
        "1.0.0",
        "V3.5.0.0.0",
        "V3..",
        "V-1",
        "V9999999999999999",
        "Dan_rocks_bigtime",
        "v00001.0"
    };

    [Fact]
    public void GatherVersions10DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v1.0", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(3);
        returnedVersions[0].RegistryKey.ShouldBe("v1.0");
        returnedVersions[1].RegistryKey.ShouldBe("v1");
        returnedVersions[2].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions20DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v2.0", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(4);
        returnedVersions[0].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[1].RegistryKey.ShouldBe("v1.0");
        returnedVersions[2].RegistryKey.ShouldBe("v1");
        returnedVersions[3].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions30DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.0", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(7);

        returnedVersions[0].RegistryKey.ShouldBe("v3.0");
        returnedVersions[1].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[2].RegistryKey.ShouldBe("v1.0");
        returnedVersions[3].RegistryKey.ShouldBe("v1");
        returnedVersions[4].RegistryKey.ShouldBe("v00001.0");
        returnedVersions[5].RegistryKey.ShouldBe("v3.0SP1");
        returnedVersions[6].RegistryKey.ShouldBe("v3.0 BAZ");
    }

    [Fact]
    public void GatherVersionsVDotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(27);

        returnedVersions[0].RegistryKey.ShouldBe("v5.0");
        returnedVersions[1].RegistryKey.ShouldBe("v5");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0001.0");
        returnedVersions[3].RegistryKey.ShouldBe("v4.1");
        returnedVersions[4].RegistryKey.ShouldBe("v4.0.255.87");
        returnedVersions[5].RegistryKey.ShouldBe("v4.0.255");
        returnedVersions[6].RegistryKey.ShouldBe("v4.0.0000");
        returnedVersions[7].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[8].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[9].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[10].RegistryKey.ShouldBe("v4.0");
        returnedVersions[11].RegistryKey.ShouldBe("v3.5");
        returnedVersions[12].RegistryKey.ShouldBe("v3.0");
        returnedVersions[13].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[14].RegistryKey.ShouldBe("v1.0");
        returnedVersions[15].RegistryKey.ShouldBe("v1");
        returnedVersions[16].RegistryKey.ShouldBe("v00001.0");
        returnedVersions[17].RegistryKey.ShouldBe("v3.0SP1");
        returnedVersions[18].RegistryKey.ShouldBe("v3.0 BAZ");
        returnedVersions[19].RegistryKey.ShouldBe("v3.5.0.x86chk");
        returnedVersions[20].RegistryKey.ShouldBe("v3.5.1.x86chk");
        returnedVersions[21].RegistryKey.ShouldBe("v3.5.256.x86chk");
        returnedVersions[22].RegistryKey.ShouldBe("v");
        returnedVersions[23].RegistryKey.ShouldBe("V3.5.0.0.0");
        returnedVersions[24].RegistryKey.ShouldBe("V3..");
        returnedVersions[25].RegistryKey.ShouldBe("V-1");
        returnedVersions[26].RegistryKey.ShouldBe("v9999999999999999", StringCompareShould.IgnoreCase);
    }

    [Fact]
    public void GatherVersions35DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.5", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(10);
        returnedVersions[0].RegistryKey.ShouldBe("v3.5");
        returnedVersions[1].RegistryKey.ShouldBe("v3.0");
        returnedVersions[2].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[3].RegistryKey.ShouldBe("v1.0");
        returnedVersions[4].RegistryKey.ShouldBe("v1");
        returnedVersions[5].RegistryKey.ShouldBe("v00001.0");
        returnedVersions[6].RegistryKey.ShouldBe("v3.5.0.x86chk");
        returnedVersions[7].RegistryKey.ShouldBe("v3.5.1.x86chk");
        returnedVersions[8].RegistryKey.ShouldBe("v3.5.256.x86chk");
        returnedVersions[9].RegistryKey.ShouldBe("V3.5.0.0.0");
    }

    [Fact]
    public void GatherVersions40DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(10);
        returnedVersions[0].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[1].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[3].RegistryKey.ShouldBe("v4.0");
        returnedVersions[4].RegistryKey.ShouldBe("v3.5");
        returnedVersions[5].RegistryKey.ShouldBe("v3.0");
        returnedVersions[6].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[7].RegistryKey.ShouldBe("v1.0");
        returnedVersions[8].RegistryKey.ShouldBe("v1");
        returnedVersions[9].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions400DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0.0", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(11);
        returnedVersions[0].RegistryKey.ShouldBe("v4.0.0000");
        returnedVersions[1].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[3].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[4].RegistryKey.ShouldBe("v4.0");
        returnedVersions[5].RegistryKey.ShouldBe("v3.5");
        returnedVersions[6].RegistryKey.ShouldBe("v3.0");
        returnedVersions[7].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[8].RegistryKey.ShouldBe("v1.0");
        returnedVersions[9].RegistryKey.ShouldBe("v1");
        returnedVersions[10].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions41DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.1", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(14);

        returnedVersions[0].RegistryKey.ShouldBe("v4.1");
        returnedVersions[1].RegistryKey.ShouldBe("v4.0.255.87");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0.255");
        returnedVersions[3].RegistryKey.ShouldBe("v4.0.0000");
        returnedVersions[4].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[5].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[6].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[7].RegistryKey.ShouldBe("v4.0");
        returnedVersions[8].RegistryKey.ShouldBe("v3.5");
        returnedVersions[9].RegistryKey.ShouldBe("v3.0");
        returnedVersions[10].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[11].RegistryKey.ShouldBe("v1.0");
        returnedVersions[12].RegistryKey.ShouldBe("v1");
        returnedVersions[13].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions410DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.1.0", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(15);

        returnedVersions[0].RegistryKey.ShouldBe("v4.0001.0");
        returnedVersions[1].RegistryKey.ShouldBe("v4.1");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0.255.87");
        returnedVersions[3].RegistryKey.ShouldBe("v4.0.255");
        returnedVersions[4].RegistryKey.ShouldBe("v4.0.0000");
        returnedVersions[5].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[6].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[7].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[8].RegistryKey.ShouldBe("v4.0");
        returnedVersions[9].RegistryKey.ShouldBe("v3.5");
        returnedVersions[10].RegistryKey.ShouldBe("v3.0");
        returnedVersions[11].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[12].RegistryKey.ShouldBe("v1.0");
        returnedVersions[13].RegistryKey.ShouldBe("v1");
        returnedVersions[14].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions40255DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0.255", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(13);
        returnedVersions[0].RegistryKey.ShouldBe("v4.0.255.87");
        returnedVersions[1].RegistryKey.ShouldBe("v4.0.255");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0.0000");
        returnedVersions[3].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[4].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[5].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[6].RegistryKey.ShouldBe("v4.0");
        returnedVersions[7].RegistryKey.ShouldBe("v3.5");
        returnedVersions[8].RegistryKey.ShouldBe("v3.0");
        returnedVersions[9].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[10].RegistryKey.ShouldBe("v1.0");
        returnedVersions[11].RegistryKey.ShouldBe("v1");
        returnedVersions[12].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions5DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v5.0", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(17);

        returnedVersions[0].RegistryKey.ShouldBe("v5.0");
        returnedVersions[1].RegistryKey.ShouldBe("v5");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0001.0");
        returnedVersions[3].RegistryKey.ShouldBe("v4.1");
        returnedVersions[4].RegistryKey.ShouldBe("v4.0.255.87");
        returnedVersions[5].RegistryKey.ShouldBe("v4.0.255");
        returnedVersions[6].RegistryKey.ShouldBe("v4.0.0000");
        returnedVersions[7].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[8].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[9].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[10].RegistryKey.ShouldBe("v4.0");
        returnedVersions[11].RegistryKey.ShouldBe("v3.5");
        returnedVersions[12].RegistryKey.ShouldBe("v3.0");
        returnedVersions[13].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[14].RegistryKey.ShouldBe("v1.0");
        returnedVersions[15].RegistryKey.ShouldBe("v1");
        returnedVersions[16].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersionsv5DotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v5", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.Count.ShouldBe(17);

        returnedVersions[0].RegistryKey.ShouldBe("v5.0");
        returnedVersions[1].RegistryKey.ShouldBe("v5");
        returnedVersions[2].RegistryKey.ShouldBe("v4.0001.0");
        returnedVersions[3].RegistryKey.ShouldBe("v4.1");
        returnedVersions[4].RegistryKey.ShouldBe("v4.0.255.87");
        returnedVersions[5].RegistryKey.ShouldBe("v4.0.255");
        returnedVersions[6].RegistryKey.ShouldBe("v4.0.0000");
        returnedVersions[7].RegistryKey.ShouldBe("v4.0.9999");
        returnedVersions[8].RegistryKey.ShouldBe("v4.0.2116.87");
        returnedVersions[9].RegistryKey.ShouldBe("v4.0.2116");
        returnedVersions[10].RegistryKey.ShouldBe("v4.0");
        returnedVersions[11].RegistryKey.ShouldBe("v3.5");
        returnedVersions[12].RegistryKey.ShouldBe("v3.0");
        returnedVersions[13].RegistryKey.ShouldBe("v2.0.50727");
        returnedVersions[14].RegistryKey.ShouldBe("v1.0");
        returnedVersions[15].RegistryKey.ShouldBe("v1");
        returnedVersions[16].RegistryKey.ShouldBe("v00001.0");
    }

    [Fact]
    public void GatherVersions35x86chkDotNet()
    {
        List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.5.0.x86chk", s_assemblyFolderExTestVersions);

        returnedVersions.ShouldNotBeNull();
        returnedVersions.ShouldHaveSingleItem();

        returnedVersions[0].RegistryKey.ShouldBe("v3.5.0.x86chk");
    }
}
#endif
