// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    public class TaskEnvironmentExtensions_Tests
    {
        private static readonly string s_projectDir =
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static TaskEnvironment CreateTaskEnvironment()
            => TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(s_projectDir);

        [Fact]
        public void GetCanonicalFormNoThrow_ResolvesRelativeSegments()
        {
            string pathWithDotDot = Path.Combine(s_projectDir, "a", "..", "b");
            var input = new AbsolutePath(pathWithDotDot, ignoreRootedCheck: true);

            AbsolutePath result = input.GetCanonicalFormNoThrow();

            result.Value.ShouldBe(Path.GetFullPath(pathWithDotDot));
        }

        [Fact]
        public void GetAbsolutePathsOrNull_NullEmptyAndRelativeEntries()
        {
            TaskEnvironment env = CreateTaskEnvironment();

            env.GetAbsolutePathsOrNull(null).ShouldBeNull();

            AbsolutePath[]? result = env.GetAbsolutePathsOrNull(new[] { null!, string.Empty, "foo.txt" });

            result.ShouldNotBeNull();
            result!.Length.ShouldBe(3);
            result[0].Value.ShouldBeNull();
            result[1].Value.ShouldBe(string.Empty);
            Path.IsPathRooted(result[2].Value).ShouldBeTrue();
            result[2].OriginalValue.ShouldBe("foo.txt");
        }

        [Fact]
        public void GetAbsolutePathOrEmpty_PassesThroughNullOrEmptyAndAbsolutizesRelative()
        {
            TaskEnvironment env = CreateTaskEnvironment();

            string nullPath = env.GetAbsolutePathOrEmpty(null!);
            string emptyPath = env.GetAbsolutePathOrEmpty(string.Empty);

            nullPath.ShouldBeNull();
            emptyPath.ShouldBe(string.Empty);

            AbsolutePath absolutized = env.GetAbsolutePathOrEmpty("foo.txt");
            string absolutizedString = absolutized;

            Path.IsPathRooted(absolutized).ShouldBeTrue();
            absolutizedString.ShouldEndWith("foo.txt");
            absolutized.OriginalValue.ShouldBe("foo.txt");
        }
    }
}
