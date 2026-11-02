// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Internal;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ReservedPropertyNames_Tests
{
    [Theory]
    [InlineData(ReservedPropertyNames.ProjectDirectory, ReservedPropertyKind.ProjectDirectory)]
    [InlineData(ReservedPropertyNames.ProjectDirectoryNoRoot, ReservedPropertyKind.ProjectDirectoryNoRoot)]
    [InlineData(ReservedPropertyNames.ProjectFile, ReservedPropertyKind.ProjectFile)]
    [InlineData(ReservedPropertyNames.ProjectExtension, ReservedPropertyKind.ProjectExtension)]
    [InlineData(ReservedPropertyNames.ProjectFullPath, ReservedPropertyKind.ProjectFullPath)]
    [InlineData(ReservedPropertyNames.ProjectName, ReservedPropertyKind.ProjectName)]
    [InlineData(ReservedPropertyNames.ThisFile, ReservedPropertyKind.ThisFile)]
    [InlineData(ReservedPropertyNames.ThisFileDirectory, ReservedPropertyKind.ThisFileDirectory)]
    [InlineData(ReservedPropertyNames.ThisFileDirectoryNoRoot, ReservedPropertyKind.ThisFileDirectoryNoRoot)]
    [InlineData(ReservedPropertyNames.ThisFileExtension, ReservedPropertyKind.ThisFileExtension)]
    [InlineData(ReservedPropertyNames.ThisFileFullPath, ReservedPropertyKind.ThisFileFullPath)]
    [InlineData(ReservedPropertyNames.ThisFileName, ReservedPropertyKind.ThisFileName)]
    [InlineData(ReservedPropertyNames.BinPath, ReservedPropertyKind.BinPath)]
    [InlineData(ReservedPropertyNames.ProjectDefaultTargets, ReservedPropertyKind.ProjectDefaultTargets)]
    [InlineData(ReservedPropertyNames.ToolsPath, ReservedPropertyKind.ToolsPath)]
    [InlineData(ReservedPropertyNames.ToolsVersion, ReservedPropertyKind.ToolsVersion)]
    [InlineData(ReservedPropertyNames.MSBuildRuntimeType, ReservedPropertyKind.RuntimeType)]
    [InlineData(ReservedPropertyNames.StartupDirectory, ReservedPropertyKind.StartupDirectory)]
    [InlineData(ReservedPropertyNames.BuildNodeCount, ReservedPropertyKind.BuildNodeCount)]
    [InlineData(ReservedPropertyNames.LastTaskResult, ReservedPropertyKind.LastTaskResult)]
    [InlineData(ReservedPropertyNames.ProgramFiles32, ReservedPropertyKind.ProgramFiles32)]
    [InlineData(ReservedPropertyNames.AssemblyVersion, ReservedPropertyKind.AssemblyVersion)]
    [InlineData(ReservedPropertyNames.Version, ReservedPropertyKind.Version)]
    [InlineData(ReservedPropertyNames.Interactive, ReservedPropertyKind.Interactive)]
    [InlineData(ReservedPropertyNames.MSBuildDisableFeaturesFromVersion, ReservedPropertyKind.DisableFeaturesFromVersion)]
    internal void TryGetKindRecognizesReservedProperties(string propertyName, ReservedPropertyKind expected)
    {
        ReservedPropertyNames.TryGetKind(propertyName, out var actual).ShouldBeTrue();
        actual.ShouldBe(expected);
        ReservedPropertyNames.IsReservedProperty(propertyName).ShouldBeTrue();

        string expression = $"$({propertyName})";
        ReservedPropertyNames.TryGetKind(expression, 2, propertyName.Length, out actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void TryGetKindIgnoresCase()
    {
        ReservedPropertyNames.TryGetKind("msbuildprojectextension", out var actual).ShouldBeTrue();
        actual.ShouldBe(ReservedPropertyKind.ProjectExtension);
        ReservedPropertyNames.IsReservedProperty("msbuildprojectextension").ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("MSBuildUnknown")]
    [InlineData(ReservedPropertyNames.ExtensionsPath)]
    [InlineData(ReservedPropertyNames.ExtensionsPath32)]
    [InlineData(ReservedPropertyNames.ExtensionsPath64)]
    [InlineData(ReservedPropertyNames.UserExtensionsPath)]
    [InlineData(ReservedPropertyNames.OverrideTasksPath)]
    [InlineData(ReservedPropertyNames.DefaultOverrideToolsVersion)]
    [InlineData(ReservedPropertyNames.ExtensionsPathSuffix)]
    [InlineData(ReservedPropertyNames.UserExtensionsPathSuffix)]
    [InlineData(ReservedPropertyNames.LocalAppData)]
    [InlineData(ReservedPropertyNames.FileVersion)]
    [InlineData(ReservedPropertyNames.SemanticVersion)]
    [InlineData(ReservedPropertyNames.OSName)]
    [InlineData(ReservedPropertyNames.FrameworkToolsRoot)]
    public void TryGetKindRejectsOtherProperties(string propertyName)
    {
        ReservedPropertyNames.TryGetKind(propertyName, out var actual).ShouldBeFalse();
        actual.ShouldBe(ReservedPropertyKind.None);
        ReservedPropertyNames.IsReservedProperty(propertyName).ShouldBeFalse();

        string expression = $"$({propertyName})";
        ReservedPropertyNames.TryGetKind(expression, 2, propertyName.Length, out actual).ShouldBeFalse();
        actual.ShouldBe(ReservedPropertyKind.None);
    }
}
