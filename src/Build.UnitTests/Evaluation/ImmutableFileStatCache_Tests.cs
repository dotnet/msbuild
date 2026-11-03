// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class ImmutableFileStatCache_Tests
{
    private static readonly DateTime s_oldTimestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly ITestOutputHelper _output;

    public ImmutableFileStatCache_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ExistingFileUnderRootIsReadOnceUntilTheNextBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder root = env.CreateFolder();
        string file = env.CreateFile(root, "shared.props", "<Project />").Path;
        var cache = new ImmutableFileStatCache([root.Path]);
        EvaluationInputs first = Record(r => r.RecordPath(file));
        EvaluationInputs second = Record(r => r.RecordPath(file));

        IsCurrent(first, cache).ShouldBeTrue();
        File.WriteAllText(file, "<Project><PropertyGroup /></Project>");

        // The second project reuses the metadata the first one read instead of reading the file again.
        IsCurrent(second, cache).ShouldBeTrue();
        cache.NotifyBuildStarted();
        IsCurrent(second, cache).ShouldBeFalse();
    }

    [Fact]
    public void FileOutsideRootsIsReadForEveryValidation()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder root = env.CreateFolder();
        string file = env.CreateFile("generated.props", "<Project />").Path;
        var cache = new ImmutableFileStatCache([root.Path]);
        EvaluationInputs inputs = Record(r => r.RecordPath(file));

        IsCurrent(inputs, cache).ShouldBeTrue();
        File.WriteAllText(file, "<Project><PropertyGroup /></Project>");

        IsCurrent(inputs, cache).ShouldBeFalse();
    }

    [Fact]
    public void SiblingDirectorySharingTheRootPrefixIsNotUnderTheRoot()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder parent = env.CreateFolder();
        string root = Path.Combine(parent.Path, "packages");
        string sibling = Path.Combine(parent.Path, "packages-generated");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(sibling);
        string file = Path.Combine(sibling, "generated.props");
        File.WriteAllText(file, "<Project />");
        var cache = new ImmutableFileStatCache([root]);
        EvaluationInputs inputs = Record(r => r.RecordPath(file));

        IsCurrent(inputs, cache).ShouldBeTrue();
        File.WriteAllText(file, "<Project><PropertyGroup /></Project>");

        IsCurrent(inputs, cache).ShouldBeFalse();
    }

    [Fact]
    public void MissingFileUnderRootIsNotShared()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder root = env.CreateFolder();
        string file = Path.Combine(root.Path, "appears.props");
        var cache = new ImmutableFileStatCache([root.Path]);
        EvaluationInputs inputs = Record(r => r.RecordProbe(file, ProbeKind.File, exists: false));

        IsCurrent(inputs, cache).ShouldBeTrue();
        File.WriteAllText(file, "<Project />");

        IsCurrent(inputs, cache).ShouldBeFalse();
    }

    [Fact]
    public void DirectoryUnderRootIsNotShared()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder root = env.CreateFolder();
        string directory = Path.Combine(root.Path, "listed");
        Directory.CreateDirectory(directory);
        Directory.SetLastWriteTimeUtc(directory, s_oldTimestamp);
        var cache = new ImmutableFileStatCache([root.Path]);
        EvaluationInputs inputs = Record(r => r.RecordGlobDirectory(directory));

        IsCurrent(inputs, cache).ShouldBeTrue();
        File.WriteAllText(Path.Combine(directory, "added.cs"), string.Empty);
        Directory.SetLastWriteTimeUtc(directory, s_oldTimestamp.AddDays(1));

        IsCurrent(inputs, cache).ShouldBeFalse();
    }

    [Fact]
    public void MissingPathUnderProbeRootIsReadOnceUntilTheNextBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder root = env.CreateFolder();
        string file = Path.Combine(root.Path, "ImportAfter", "hook.props");
        var cache = new ImmutableFileStatCache([], [root.Path]);
        EvaluationInputs first = Record(r => r.RecordProbe(file, ProbeKind.File, exists: false));
        EvaluationInputs second = Record(r => r.RecordProbe(file, ProbeKind.File, exists: false));

        IsCurrent(first, cache).ShouldBeTrue();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "<Project />");

        // The second project reuses the absence the first one read instead of probing again.
        IsCurrent(second, cache).ShouldBeTrue();
        cache.NotifyBuildStarted();
        IsCurrent(second, cache).ShouldBeFalse();
    }

    [Fact]
    public void DirectoryUnderProbeRootIsReadOnceUntilTheNextBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder root = env.CreateFolder();
        string directory = Path.Combine(root.Path, "listed");
        Directory.CreateDirectory(directory);
        Directory.SetLastWriteTimeUtc(directory, s_oldTimestamp);
        var cache = new ImmutableFileStatCache([], [root.Path]);
        EvaluationInputs inputs = Record(r => r.RecordGlobDirectory(directory));

        IsCurrent(inputs, cache).ShouldBeTrue();
        File.WriteAllText(Path.Combine(directory, "added.cs"), string.Empty);
        Directory.SetLastWriteTimeUtc(directory, s_oldTimestamp.AddDays(1));

        IsCurrent(inputs, cache).ShouldBeTrue();
        cache.NotifyBuildStarted();
        IsCurrent(inputs, cache).ShouldBeFalse();
    }

    [Fact]
    public void ProbeRootDoesNotShareMissingPathsUnderAnotherRoot()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder probeRoot = env.CreateFolder();
        TransientTestFolder packageRoot = env.CreateFolder();
        string restored = Path.Combine(packageRoot.Path, "package", "build", "package.props");
        var cache = new ImmutableFileStatCache([packageRoot.Path], [probeRoot.Path]);
        EvaluationInputs inputs = Record(r => r.RecordProbe(restored, ProbeKind.File, exists: false));

        IsCurrent(inputs, cache).ShouldBeTrue();
        Directory.CreateDirectory(Path.GetDirectoryName(restored)!);
        File.WriteAllText(restored, "<Project />");

        IsCurrent(inputs, cache).ShouldBeFalse();
    }

    [Fact]
    public void UserExtensionsPathFollowsLocalAppData()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder localAppData = env.CreateFolder();
        env.SetEnvironmentVariable("LOCALAPPDATA", localAppData.Path);

        ImmutableFileStatCache.ComputeUserExtensionsPath(toolsDirectory: null)
            .ShouldBe(Path.Combine(localAppData.Path, "Microsoft", "MSBuild"));
    }

    [Fact]
    public void DefaultRootsShareTheUserExtensionsHooksUntilTheNextBuild()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder localAppData = env.CreateFolder();
        env.SetEnvironmentVariable("LOCALAPPDATA", localAppData.Path);
        string hooks = Path.Combine(localAppData.Path, "Microsoft", "MSBuild", "Current", "Imports", "Microsoft.Common.props", "ImportBefore");
        (string[] roots, string[] probeRoots) = ImmutableFileStatCache.ComputeDefaultRoots();
        var cache = new ImmutableFileStatCache(roots, probeRoots);
        EvaluationInputs inputs = Record(r => r.RecordProbe(hooks, ProbeKind.Directory, exists: false));

        IsCurrent(inputs, cache).ShouldBeTrue();
        Directory.CreateDirectory(hooks);

        // Every project in the build reuses the absence the first one read; the next build sees the new hook.
        IsCurrent(inputs, cache).ShouldBeTrue();
        cache.NotifyBuildStarted();
        IsCurrent(inputs, cache).ShouldBeFalse();
    }

    private static EvaluationInputs Record(Action<EvaluationInputRecorder> record)
    {
        var recorder = new EvaluationInputRecorder();
        record(recorder);
        var key = new ProjectInstanceSnapshotCacheKey(
            Path.Combine(Path.GetTempPath(), "file-stat-cache-tests", "project.csproj"),
            "Current",
            false,
            null,
            ProjectLoadSettings.Default,
            new Dictionary<string, string>());
        return recorder.Freeze(key.ToEvaluationInputKey());
    }

    private static bool IsCurrent(EvaluationInputs inputs, ImmutableFileStatCache cache) =>
        EvaluationInputValidator.IsFileSystemCurrent(inputs, out _, cache);
}
