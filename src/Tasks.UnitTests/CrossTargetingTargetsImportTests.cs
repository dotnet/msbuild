// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;

using Shouldly;

using Xunit;

namespace Microsoft.Build.Tasks.UnitTests;

/// <summary>
/// Tests for the NuGet restore targets import in <c>Microsoft.Common.CrossTargeting.targets</c>.
/// The parallel import in <c>Microsoft.Common.CurrentVersion.targets</c> is guarded with
/// <c>Exists('$(NuGetRestoreTargets)')</c>, but for years the cross-targeting variant was unguarded
/// — which meant cross-targeting (multi-TFM) projects loaded in non-NuGet contexts (custom build
/// systems, focused unit tests) failed with "Imported project not found" unless the caller
/// manufactured a stub NuGet.targets file. These tests cover the now-guarded import.
/// </summary>
public sealed class CrossTargetingTargetsImportTests
{
    private readonly ITestOutputHelper _output;

    public CrossTargetingTargetsImportTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// When <c>NuGetRestoreTargets</c> points at a file that does not exist, evaluating a project
    /// that imports <c>Microsoft.Common.CrossTargeting.targets</c> must succeed (the import is a
    /// silent no-op), matching the long-standing behavior of <c>Microsoft.Common.CurrentVersion.targets</c>.
    /// </summary>
    [Fact]
    public void ImportSucceedsWhenNuGetRestoreTargetsDoesNotExist()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateFolder(createFolder: true);
        string nonExistentNuGetTargets = Path.Combine(folder.Path, "DoesNotExist.NuGet.targets");

        string projectContents = $"""
            <Project>
                <PropertyGroup>
                    <NuGetRestoreTargets>{nonExistentNuGetTargets}</NuGetRestoreTargets>
                </PropertyGroup>
                <Import Project="$(MSBuildToolsPath)\Microsoft.Common.CrossTargeting.targets" />
            </Project>
            """.Cleanup();

        using ProjectFromString projectFromString = new(projectContents);
        Project project = projectFromString.Project;

        project.GetPropertyValue("NuGetRestoreTargets").ShouldBe(nonExistentNuGetTargets);
    }

    /// <summary>
    /// When <c>NuGetRestoreTargets</c> points at a real file, the import still happens. This
    /// guards against the new <c>Exists()</c> condition accidentally skipping a valid NuGet
    /// targets file. The synthetic project does not set <c>IsRestoreTargetsFileLoaded</c>, and
    /// nothing in the <c>Microsoft.Common.CrossTargeting.targets</c> import chain sets it before
    /// the guarded import, so the canonical guard does not short-circuit here.
    /// </summary>
    [Fact]
    public void ImportLoadsNuGetTargetsWhenItExists()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateFolder(createFolder: true);

        const string MarkerTargetName = "_CrossTargetingNuGetStubMarker";
        TransientTestFile nuGetStub = env.CreateFile(
            folder,
            "NuGet.targets",
            $"""
            <Project>
                <Target Name="{MarkerTargetName}" />
            </Project>
            """.Cleanup());

        string projectContents = $"""
            <Project>
                <PropertyGroup>
                    <NuGetRestoreTargets>{nuGetStub.Path}</NuGetRestoreTargets>
                </PropertyGroup>
                <Import Project="$(MSBuildToolsPath)\Microsoft.Common.CrossTargeting.targets" />
            </Project>
            """.Cleanup();

        using ProjectFromString projectFromString = new(projectContents);
        Project project = projectFromString.Project;

        project.Targets.ShouldContainKey(MarkerTargetName);
    }
}

