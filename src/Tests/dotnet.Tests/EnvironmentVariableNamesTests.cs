// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace dotnet.Tests;

public class EnvironmentVariableNamesTests
{
    [Theory]
    [InlineData("os.1.2-x86", Architecture.X86)]
    [InlineData("os.1.2-x64", Architecture.X64)]
    [InlineData("os.1.2-arm", Architecture.Arm)]
    [InlineData("os.1.2-arm64", Architecture.Arm64)]
    [InlineData("os.1.2-wasm", Architecture.Wasm)]
    [InlineData("os.1.2-s390x", Architecture.S390x)]
    [InlineData("os.1.2-loongarch64", Architecture.LoongArch64)]
    [InlineData("os.1.2-armv6", Architecture.Armv6)]
    [InlineData("os.1.2-ppc64le", Architecture.Ppc64le)]
    [InlineData("os.1.2-lOOngaRch64", Architecture.LoongArch64)] // case-insensitive
    [InlineData("os-x86", Architecture.X86)]
    [InlineData("-x86", Architecture.X86)]
    [InlineData("-x86-", Architecture.X86)]
    public static void TryParseArchitecture(string rid, Architecture expected)
    {
        Assert.True(EnvironmentVariableNames.TryParseArchitecture(rid, out var actual));
        Assert.Equal(expected, actual);

        Assert.True(EnvironmentVariableNames.TryParseArchitecture(rid + "-xyz", out actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("---")]
    [InlineData("x86")]
    [InlineData("os")]
    [InlineData("os.")]
    [InlineData("os.1")]
    [InlineData("os.1.2")]
    [InlineData("os.1.2-")]
    [InlineData("os.1.2--")]
    [InlineData("os.1.2-unknown")]
    [InlineData("os.1.2-unknown-")]
    [InlineData("os.1.2-unknown-x")]
    [InlineData("os.1.2-armel")] // currently not defined
    public static void TryParseArchitecture_Invalid(string rid)
    {
        Assert.False(EnvironmentVariableNames.TryParseArchitecture(rid, out _));
    }

    [Theory]
    [InlineData("os-x86", null, Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData("os-x86", null, Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("os-x86", "v5.0", Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData("os-x86", "v5.0", Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("os-x86", "v6.0", Architecture.X86, true, "DOTNET_ROOT_X86")]
    [InlineData("os-x86", "v6.0", Architecture.X86, false, "DOTNET_ROOT_X86")]
    [InlineData("os-x64", "v5.0", Architecture.X64, true, "DOTNET_ROOT")]
    [InlineData("os-x64", "v6.0", Architecture.X64, true, "DOTNET_ROOT_X64")]
    [InlineData("os-arm64", "v6.0", Architecture.Arm64, true, "DOTNET_ROOT_ARM64")]
    [InlineData("os-armv6", "v6.0", Architecture.Armv6, true, "DOTNET_ROOT_ARMV6")]
    [InlineData("os-armv6", "v6.0", Architecture.Arm64, true, null)]
    [InlineData("os-x64", "v6.0", Architecture.X86, false, null)]
    public static void TryGetDotNetRootVariableName_KnownArchitecture(string rid, string frameworkVersion, Architecture currentArchitecture, bool is64bit, string expected)
    {
        var parsedVersion = EnvironmentVariableNames.TryParseTargetFrameworkVersion(frameworkVersion);
        Assert.Equal(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl(rid, "os-unknown", parsedVersion, currentArchitecture, is64bit));
        Assert.Equal(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl(rid, "os-armv6", parsedVersion, currentArchitecture, is64bit));
        Assert.Equal(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl("os-unknown", rid, parsedVersion, currentArchitecture, is64bit));
    }

    [Theory]
    [InlineData(null, Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData(null, Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("v5.0", Architecture.X86, true, "DOTNET_ROOT")]
    [InlineData("v5.0", Architecture.X86, false, "DOTNET_ROOT(x86)")]
    [InlineData("v6.0", Architecture.X86, true, "DOTNET_ROOT_X86")]
    [InlineData("v6.0", Architecture.X86, false, "DOTNET_ROOT_X86")]
    [InlineData("v5.0", Architecture.X64, true, "DOTNET_ROOT")]
    [InlineData("v6.0", Architecture.X64, true, "DOTNET_ROOT_X64")]
    [InlineData("v6.0", Architecture.Arm64, true, "DOTNET_ROOT_ARM64")]
    [InlineData("v6.0", Architecture.Armv6, true, "DOTNET_ROOT_ARMV6")]
    [InlineData("v6.0", Architecture.Wasm, true, "DOTNET_ROOT_WASM")]
    [InlineData("v6.0", Architecture.Wasm, false, "DOTNET_ROOT_WASM")]
    public static void TryGetDotNetRootVariableName_UnknownArchitecture(string frameworkVersion, Architecture currentArchitecture, bool is64bit, string expected)
    {
        var parsedVersion = EnvironmentVariableNames.TryParseTargetFrameworkVersion(frameworkVersion);
        Assert.Equal(expected, EnvironmentVariableNames.TryGetDotNetRootVariableNameImpl("os-unknown", "os-unknown", parsedVersion, currentArchitecture, is64bit));
    }
}
