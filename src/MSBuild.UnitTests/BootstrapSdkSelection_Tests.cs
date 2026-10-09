// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class BootstrapSdkSelection_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void BootstrapSdkSelectionUsesMetadataInsteadOfPinnedOrHighestInstalledSdk()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string[] versions = ["11.0.100", "12.0.100-daily.1", "13.0.100"];
        foreach (string version in versions)
        {
            Directory.CreateDirectory(Path.Combine(root, "core", "sdk", version));
        }

        File.WriteAllText(Path.Combine(root, "core", "bootstrap-sdk.version"), "12.0.100-daily.1\n");

        object attribute = CreateBootstrapLocation(root);
        ReadVersion(attribute).ShouldBe("12.0.100-daily.1");
    }

    [Fact]
    public void BootstrapSdkSelectionPreservesLegacyPinnedLayoutsWithoutMetadata()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        object attribute = CreateBootstrapLocation(env.CreateFolder().Path);

        ReadVersion(attribute).ShouldBe("11.0.100");
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../12.0.100")]
    [InlineData(@"..\12.0.100")]
    [InlineData("12.0.100-missing")]
    public void BootstrapSdkSelectionRejectsInvalidMetadataInsteadOfFallingBack(string version)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        Directory.CreateDirectory(Path.Combine(root, "core", "sdk", "11.0.100"));
        File.WriteAllText(Path.Combine(root, "core", "bootstrap-sdk.version"), version);

        TargetInvocationException exception = Should.Throw<TargetInvocationException>(() => CreateBootstrapLocation(root));

        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
        exception.InnerException!.Message.ShouldContain("bootstrap-sdk.version");
    }

    [Fact]
    public void BootstrapSdkSelectionPinsResolverAndHostPathsToSelectedCoreSdk()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        Directory.CreateDirectory(Path.Combine(root, "core", "sdk", "12.0.100-daily.1"));
        File.WriteAllText(Path.Combine(root, "core", "bootstrap-sdk.version"), "12.0.100-daily.1");
        object attribute = CreateBootstrapLocation(root);
        Dictionary<string, string?> variables = [];

        typeof(RunnerUtilities).GetMethod("ApplyBootstrapSdkOverrides", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [variables, attribute]);

        string core = Path.Combine(root, "core");
        string sdks = Path.Combine(core, "sdk", "12.0.100-daily.1", "Sdks");
        variables["MSBuildSDKsPath"].ShouldBe(sdks);
        variables["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"].ShouldBe(sdks);
        variables["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER"].ShouldBe("12.0.100-daily.1");
        variables["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"].ShouldBe(core);
        variables["DOTNET_ROOT"].ShouldBe(core);
        variables["DOTNET_HOST_PATH"].ShouldBe(EnvironmentProvider.GetDotnetExePathFromFolder(core));
        variables["DOTNET_ROOT_X86"].ShouldBeNull();
        variables["DOTNET_ROOT_X64"].ShouldBeNull();
        variables["DOTNET_ROOT_ARM64"].ShouldBeNull();
    }

    private static object CreateBootstrapLocation(string root)
    {
        Type type = typeof(RunnerUtilities).Assembly.GetType("Microsoft.Build.UnitTests.Shared.BootstrapLocationAttribute", throwOnError: true)!;
        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [root, Path.Combine(root, "net472", "MSBuild", "Current", "Bin"), "11.0.100", "net11.0"],
            culture: null)!;
    }

    private static string ReadVersion(object attribute)
        => (string)attribute.GetType().GetProperty(nameof(RunnerUtilities.BootstrapSdkVersion))!.GetValue(attribute)!;
}
