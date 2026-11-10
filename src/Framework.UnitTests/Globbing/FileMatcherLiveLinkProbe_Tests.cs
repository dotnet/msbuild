// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
#if FEATURE_SYMLINK_TARGET
using System.Reflection;
using Microsoft.Build.Framework;
#endif
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Globbing;

public sealed class FileMatcherLiveLinkProbe_Tests
{
    private readonly ITestOutputHelper _output;
#if FEATURE_SYMLINK_TARGET
    private static readonly Func<string, string, StringComparison, bool> IsSubdirectoryOf =
        (Func<string, string, StringComparison, bool>)typeof(FileMatcher)
            .GetMethod("IsSubdirectoryOf", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Func<string, string, StringComparison, bool>));
#endif

    public FileMatcherLiveLinkProbe_Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void MissingAndInvalidPathsRetainOriginalDisposition()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        TransientTestFolder folder = environment.CreateFolder();
        string root = folder.Path;
        string file = environment.CreateFile(folder, fileName: "ordinary.txt").Path;
        string[] paths =
        [
            root, root + Path.DirectorySeparatorChar, file, file + Path.DirectorySeparatorChar,
            Path.Combine(root, "missing"), Path.Combine(file, "child"), "", "invalid\0path",
            Path.Combine(root, "*.txt"), Path.Combine(root, "?.txt"), Path.GetPathRoot(root)!
        ];

        foreach (string path in paths)
        {
            FileMatcher.ShouldSkipRecursiveDirectory(path).ShouldBe(OriginalShouldSkip(path), path);
        }
        FileMatcher.ShouldSkipRecursiveDirectory(null!).ShouldBe(OriginalShouldSkip(null!));
    }

    [Fact]
    public void RelativePathsRemainLiveAcrossWorkingDirectories()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string first = environment.CreateFolder().Path;
        string second = environment.CreateFolder().Path;
        Directory.CreateDirectory(Path.Combine(first, "directory"));
        environment.SetCurrentDirectory(first);
        FileMatcher.ShouldSkipRecursiveDirectory("directory").ShouldBe(OriginalShouldSkip("directory"));
        FileMatcher.ShouldSkipRecursiveDirectory(Path.Combine("directory", "..", "directory"))
            .ShouldBe(OriginalShouldSkip(Path.Combine("directory", "..", "directory")));
        environment.SetCurrentDirectory(second);
        FileMatcher.ShouldSkipRecursiveDirectory("directory").ShouldBe(OriginalShouldSkip("directory"));
    }

#if NET
#if FEATURE_SYMLINK_TARGET
    [WindowsOnlyFact]
    public void ShouldSkipRecursiveDirectory_OrdinaryDrivePath_QueriesAttributesWithoutResolving()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string[] paths = [root, root + Path.DirectorySeparatorChar, root.Replace('\\', '/'), Path.GetPathRoot(root)!];
        foreach (string directory in paths)
        {
            List<string> calls = [];
            FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget).ShouldBeFalse();
            calls.ShouldBe(["attributes"], directory);

            FileAttributes GetAttributes(string path)
            {
                path.ShouldBe(directory);
                calls.Add("attributes");
                FileAttributes attributes = File.GetAttributes(path);
                (attributes & FileAttributes.ReparsePoint).ShouldBe((FileAttributes)0);
                return attributes;
            }

            FileSystemInfo? ResolveLinkTarget(string path, bool returnFinalTarget)
            {
                calls.Add("resolver");
                return Directory.ResolveLinkTarget(path, returnFinalTarget);
            }
        }
    }

    [WindowsOnlyFact]
    public void ShouldSkipRecursiveDirectory_ReparsePoint_UsesFinalTargetResolver()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string directory = Path.Combine(root, "child", "ancestor");
        List<string> calls = [];
        FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget).ShouldBeTrue();
        calls.ShouldBe(["attributes", "resolver"]);

        FileAttributes GetAttributes(string path)
        {
            path.ShouldBe(directory);
            calls.Add("attributes");
            return FileAttributes.Directory | FileAttributes.ReparsePoint;
        }

        FileSystemInfo ResolveLinkTarget(string path, bool returnFinalTarget)
        {
            path.ShouldBe(directory);
            returnFinalTarget.ShouldBeTrue();
            calls.Add("resolver");
            return new DirectoryInfo(root);
        }
    }

    [WindowsOnlyTheory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(SecurityException))]
    public void ShouldSkipRecursiveDirectory_InconclusiveProbe_UsesFinalTargetResolver(Type exceptionType)
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string directory = Path.Combine(root, "child", "ancestor");
        Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;
        List<string> calls = [];
        FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget).ShouldBeTrue();
        calls.ShouldBe(["attributes", "resolver"]);

        FileAttributes GetAttributes(string path)
        {
            path.ShouldBe(directory);
            calls.Add("attributes");
            throw exception;
        }

        FileSystemInfo ResolveLinkTarget(string path, bool returnFinalTarget)
        {
            path.ShouldBe(directory);
            returnFinalTarget.ShouldBeTrue();
            calls.Add("resolver");
            return new DirectoryInfo(root);
        }
    }

    [WindowsOnlyTheory]
    [InlineData(typeof(IOException), true)]
    [InlineData(typeof(ArgumentException), true)]
    [InlineData(typeof(UnauthorizedAccessException), true)]
    [InlineData(typeof(NotSupportedException), false)]
    [InlineData(typeof(SecurityException), false)]
    public void ShouldSkipRecursiveDirectory_ResolverFailure_PreservesExceptionBoundary(Type exceptionType, bool caught)
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string directory = environment.CreateFolder().Path;
        Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;
        List<string> calls = [];
        if (caught)
        {
            FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget).ShouldBeFalse();
        }
        else
        {
            Record.Exception(() => FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget))
                .ShouldBeSameAs(exception);
        }
        calls.ShouldBe(["attributes", "resolver"]);

        FileAttributes GetAttributes(string path)
        {
            calls.Add("attributes");
            return FileAttributes.Directory | FileAttributes.ReparsePoint;
        }

        FileSystemInfo ResolveLinkTarget(string path, bool returnFinalTarget)
        {
            calls.Add("resolver");
            throw exception;
        }
    }

    [WindowsOnlyFact]
    public void ShouldSkipRecursiveDirectory_UnexpectedProbeFailure_DoesNotInvokeResolver()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string directory = environment.CreateFolder().Path;
        List<string> calls = [];
        Should.Throw<InvalidOperationException>(() =>
            FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget));
        calls.ShouldBe(["attributes"]);

        FileAttributes GetAttributes(string path)
        {
            calls.Add("attributes");
            throw new InvalidOperationException();
        }

        FileSystemInfo? ResolveLinkTarget(string path, bool returnFinalTarget)
        {
            calls.Add("resolver");
            return null;
        }
    }

    [WindowsOnlyTheory]
    [InlineData("")]
    [InlineData("directory")]
    [InlineData(@"C:relative")]
    [InlineData(@"\rooted")]
    [InlineData(@"\\server\share\directory")]
    [InlineData(@"\\?\C:\directory")]
    [InlineData(@"\\.\C:\directory")]
    [InlineData(@"C:\directory\*")]
    [InlineData(@"C:\directory\?")]
    public void ShouldSkipRecursiveDirectory_UnsupportedPathForm_OnlyInvokesResolver(string directory)
    {
        List<string> calls = [];
        FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget).ShouldBeFalse();
        calls.ShouldBe(["resolver"]);

        FileAttributes GetAttributes(string path)
        {
            calls.Add("attributes");
            return FileAttributes.Directory;
        }

        FileSystemInfo? ResolveLinkTarget(string path, bool returnFinalTarget)
        {
            path.ShouldBe(directory);
            returnFinalTarget.ShouldBeTrue();
            calls.Add("resolver");
            return null;
        }
    }

    [UnixOnlyFact]
    public void ShouldSkipRecursiveDirectory_NonWindows_OnlyInvokesResolver()
    {
        List<string> calls = [];
        FileMatcher.ShouldSkipRecursiveDirectory("/ordinary/directory", GetAttributes, ResolveLinkTarget).ShouldBeFalse();
        calls.ShouldBe(["resolver"]);

        FileAttributes GetAttributes(string path)
        {
            calls.Add("attributes");
            return FileAttributes.Directory;
        }

        FileSystemInfo? ResolveLinkTarget(string path, bool returnFinalTarget)
        {
            path.ShouldBe("/ordinary/directory");
            returnFinalTarget.ShouldBeTrue();
            calls.Add("resolver");
            return null;
        }
    }

    [RequiresSymbolicLinksFact]
    public void ShouldSkipRecursiveDirectory_RealChildThroughParentLink_ObservesChildAttributes()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string target = environment.CreateFolder().Path;
        Directory.CreateDirectory(Path.Combine(target, "child"));
        string link = Path.Combine(root, "parent-link");
        environment.WithTransientTestState(new LinkCleanup(link));
        Directory.CreateSymbolicLink(link, target);
        string directory = Path.Combine(link, "child");
        (File.GetAttributes(link) & FileAttributes.ReparsePoint).ShouldNotBe((FileAttributes)0);
        (File.GetAttributes(directory) & FileAttributes.ReparsePoint).ShouldBe((FileAttributes)0);
        Directory.ResolveLinkTarget(directory, returnFinalTarget: true).ShouldBeNull();
        List<string> calls = [];
        FileMatcher.ShouldSkipRecursiveDirectory(directory, GetAttributes, ResolveLinkTarget).ShouldBeFalse();
        calls.ShouldBe(NativeMethodsShared.IsWindows ? ["attributes"] : ["resolver"]);
        FileMatcher.ShouldSkipRecursiveDirectory(directory).ShouldBe(OriginalShouldSkip(directory));

        FileAttributes GetAttributes(string path)
        {
            path.ShouldBe(directory);
            calls.Add("attributes");
            return File.GetAttributes(path);
        }

        FileSystemInfo? ResolveLinkTarget(string path, bool returnFinalTarget)
        {
            path.ShouldBe(directory);
            returnFinalTarget.ShouldBeTrue();
            calls.Add("resolver");
            return Directory.ResolveLinkTarget(path, returnFinalTarget);
        }
    }
#endif

    [WindowsOnlyFact]
    public void LongAndExtendedPathsRetainOriginalDisposition()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string longPath = Path.Combine(root, new string('a', 100), new string('b', 100), new string('c', 100));
        Directory.CreateDirectory(longPath);
        string[] paths =
        [
            longPath, longPath + Path.DirectorySeparatorChar, @"\\?\" + longPath,
            @"\\?\" + longPath + Path.DirectorySeparatorChar, Path.Combine(longPath, "missing"),
            @"\\.\" + root, root[..2], root[..2] + "relative", root[2..],
            Path.Combine(root, ".", "..", Path.GetFileName(root)), root.Replace('\\', '/')
        ];

        foreach (string path in paths)
        {
            FileMatcher.ShouldSkipRecursiveDirectory(path).ShouldBe(OriginalShouldSkip(path), path);
        }
    }
#endif

#if FEATURE_SYMLINK_TARGET
    [RequiresSymbolicLinksFact]
    public void LinkChainsAndPathSpellingsKeepFinalTargetSemantics()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string child = Path.Combine(root, "child");
        Directory.CreateDirectory(child);
        string ancestor = Path.Combine(child, "ancestor");
        string second = Path.Combine(child, "second");
        string external = environment.CreateFolder().Path;
        string outward = Path.Combine(child, "outward");
        string broken = Path.Combine(child, "broken");
        environment.WithTransientTestState(new LinkCleanup(ancestor, second, outward, broken));
        Directory.CreateSymbolicLink(ancestor, root);
        Directory.CreateSymbolicLink(second, ancestor);
        Directory.CreateSymbolicLink(outward, external);
        Directory.CreateSymbolicLink(broken, Path.Combine(root, "missing"));
        string[] paths =
        [
            ancestor, second, outward, broken, ancestor + Path.DirectorySeparatorChar,
            second + Path.DirectorySeparatorChar, ancestor + new string(Path.DirectorySeparatorChar, 2)
        ];

        foreach (string path in paths)
        {
            FileMatcher.ShouldSkipRecursiveDirectory(path).ShouldBe(OriginalShouldSkip(path), path);
        }
        FileMatcher.ShouldSkipRecursiveDirectory(ancestor).ShouldBeTrue();
        FileMatcher.ShouldSkipRecursiveDirectory(second).ShouldBeTrue();
        FileMatcher.ShouldSkipRecursiveDirectory(outward).ShouldBeFalse();

        environment.SetCurrentDirectory(child);
        FileMatcher.ShouldSkipRecursiveDirectory("ancestor").ShouldBe(OriginalShouldSkip("ancestor"));
    }

    [RequiresSymbolicLinksFact]
    public void RepeatedObservationsSeeDirectoryChangedToAndFromLink()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string observed = Path.Combine(root, "observed");
        Directory.CreateDirectory(observed);
        environment.WithTransientTestState(new LinkCleanup(observed));

        FileMatcher.ShouldSkipRecursiveDirectory(observed).ShouldBeFalse();
        Directory.Delete(observed);
        FileMatcher.ShouldSkipRecursiveDirectory(observed).ShouldBe(OriginalShouldSkip(observed));
        Directory.CreateSymbolicLink(observed, root);
        FileMatcher.ShouldSkipRecursiveDirectory(observed).ShouldBeTrue();
        FileMatcher.ShouldSkipRecursiveDirectory(observed).ShouldBe(OriginalShouldSkip(observed));
        Directory.Delete(observed);
        Directory.CreateDirectory(observed);
        FileMatcher.ShouldSkipRecursiveDirectory(observed).ShouldBeFalse();
    }

    [RequiresSymbolicLinksFact]
    public void LinkCyclesRetainOriginalCaughtErrorDisposition()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        string first = Path.Combine(root, "first");
        string second = Path.Combine(root, "second");
        environment.WithTransientTestState(new LinkCleanup(first, second));
        Directory.CreateSymbolicLink(first, second);
        Directory.CreateSymbolicLink(second, first);
        FileMatcher.ShouldSkipRecursiveDirectory(first).ShouldBe(OriginalShouldSkip(first));
        FileMatcher.ShouldSkipRecursiveDirectory(second).ShouldBe(OriginalShouldSkip(second));
    }
#endif

    [Fact]
    public void CallbackAndDirectGlobsPreserveFilesAndFailures()
    {
        using TestEnvironment environment = TestEnvironment.Create(_output);
        string root = environment.CreateFolder().Path;
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "obj"));
        File.WriteAllText(Path.Combine(root, "root.cs"), "");
        File.WriteAllText(Path.Combine(root, "src", "child.cs"), "");
        File.WriteAllText(Path.Combine(root, "obj", "excluded.cs"), "");
        string include = Path.Combine("**", "*.cs");
        List<string> excludes = [Path.Combine("**", "obj", "**")];
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher callback = new(FileSystems.Default, new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);
        FileMatcher direct = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);
        var expected = legacy.GetFiles(root, include, excludes);
        FileMatcher[] matchers = [callback, direct];
        foreach (FileMatcher matcher in matchers)
        {
            var actual = matcher.GetFiles(root, include, excludes);
            actual.FileList.OrderBy(path => path, StringComparer.Ordinal).ShouldBe(expected.FileList.OrderBy(path => path, StringComparer.Ordinal));
            actual.Action.ShouldBe(expected.Action);
            actual.ExcludeFileSpec.ShouldBe(expected.ExcludeFileSpec);
            actual.GlobFailure.ShouldBe(expected.GlobFailure);
        }
    }

    private static bool OriginalShouldSkip(string directory)
    {
#if FEATURE_SYMLINK_TARGET
        try
        {
            FileSystemInfo? target = Directory.ResolveLinkTarget(directory, returnFinalTarget: true);
            return target is not null && IsSubdirectoryOf(directory, target.FullName, FileUtilities.PathComparison);
        }
        catch (IOException) { }
        catch (ArgumentException) { }
        catch (UnauthorizedAccessException) { }
#endif
        return false;
    }

    private sealed class LinkCleanup(params string[] paths) : TransientTestState
    {
        public override void Revert()
        {
            foreach (string path in paths.Reverse())
            {
                try
                {
                    Directory.Delete(path);
                }
                catch (DirectoryNotFoundException) { }
            }
        }
    }
}
