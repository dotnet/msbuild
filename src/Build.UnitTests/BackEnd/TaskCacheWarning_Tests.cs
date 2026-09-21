// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskCacheWarning_Tests(ITestOutputHelper output)
{
    public static bool IsSupported => TaskCacheStore.IsSupported;

    [Fact]
    public void StandardWarningPayloadRoundTripsWithoutOldInvocationIdentity()
    {
        BuildEventContext current = new(2, 3, 4, 5);
        BuildWarningEventArgs original = new("category", "WARN1", "file.cs", 1, 2, 3, 4,
            "literal {braces}", "help", "sender", "https://example.invalid/help", DateTime.UtcNow, messageArgs: null);
        TaskCacheWarning warning = TaskCacheWarning.Snapshot(original, out int size)!;
        size.ShouldBeGreaterThan(0);
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        TaskCacheWarning.Write(writer, [warning]);
        stream.Position = 0;
        using BinaryReader reader = new(stream);
        TaskCacheWarning.Read(reader).ShouldBe([warning]);
        BuildWarningEventArgs replayed = warning.ToEvent(current);
        replayed.GetType().ShouldBe(typeof(BuildWarningEventArgs));
        replayed.BuildEventContext.ShouldBe(current);
        replayed.ProjectFile.ShouldBeNull();
        replayed.HelpLink.ShouldBe(original.HelpLink);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1025)]
    [InlineData(int.MaxValue)]
    public void RejectsInvalidCountsBeforeAllocation(int count)
    {
        using MemoryStream stream = new(BitConverter.GetBytes(count));
        using BinaryReader reader = new(stream);
        Should.Throw<InvalidDataException>(() => TaskCacheWarning.Read(reader));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(1048577)]
    [InlineData(int.MaxValue)]
    public void RejectsInvalidStringLengths(int length)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(1);
        writer.Write(length);
        stream.Position = 0;
        using BinaryReader reader = new(stream);
        Should.Throw<InvalidDataException>(() => TaskCacheWarning.Read(reader));
    }

    [Fact]
    public void RejectsUnknownWarningSubclassRatherThanDroppingItsFields()
    {
        BuildWarningEventArgs warning = new ExtendedBuildWarningEventArgs("extended", null, "WARN1", null,
            1, 1, 1, 1, "message", null, "sender", null, DateTime.UtcNow, messageArgs: null);
        TaskCacheWarning.Snapshot(warning, out _).ShouldBeNull();
    }

    [Fact]
    public void PreservesNullEmptyAndUnicodeStrings()
    {
        TaskCacheWarning warning = new(null, "", null, "warning λ\n", null, "", null, 0, 0, 0, 0);
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        TaskCacheWarning.Write(writer, [warning]);
        stream.Position = 0;
        using BinaryReader reader = new(stream);
        TaskCacheWarning.Read(reader).ShouldBe([warning]);
    }

    [Fact]
    public void RejectsInvalidUtf8WithoutSubstitutingCharacters()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(1);
        writer.Write(1);
        writer.Write((byte)0xff);
        stream.Position = 0;
        using BinaryReader reader = new(stream);
        Should.Throw<InvalidDataException>(() => TaskCacheWarning.Read(reader));
    }

    [Fact]
    public void RejectsOversizedWarningStringsBeforeEncoding()
    {
        BuildWarningEventArgs warning = new(null, "WARN1", null, 0, 0, 0, 0,
            new string('x', TaskCacheWarning.MaximumStringBytes + 1), null, "task");
        Should.Throw<InvalidDataException>(() => TaskCacheWarning.Snapshot(warning, out _));
    }

    [ConditionalFact(typeof(TaskCacheWarning_Tests), nameof(IsSupported))]
    public void TruncatedWarningsFailBeforeAnyArtifactReplacement()
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        string cache = env.CreateFolder().Path;
        string path = env.CreateFile("artifact.txt", "cached").Path;
        using TaskCacheBackend backend = TaskCacheBackend.Create(cache);
        TaskCacheStore store = new(cache, backend);
        string key = new('A', 64);
        store.Store(key, [path], BitConverter.GetBytes(1)).ShouldBeTrue();
        File.WriteAllText(path, "untouched");
        Should.Throw<IOException>(() => store.TryRestore(key, [path], out _, state =>
        {
            using MemoryStream stream = new(state);
            using BinaryReader reader = new(stream);
            TaskCacheWarning.Read(reader);
        }));
        File.ReadAllText(path).ShouldBe("untouched");
        Directory.GetFiles(Path.GetDirectoryName(path)!, ".msbuild-cache-*").ShouldBeEmpty();
    }
}
