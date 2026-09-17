// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class FormatUrl_Tests
    {
        private readonly ITestOutputHelper _out;

        public FormatUrl_Tests(ITestOutputHelper testOutputHelper)
        {
            _out = testOutputHelper;
        }

        private FormatUrl GetFormatUrlUnderTest() => new FormatUrl
        {
            BuildEngine = new MockEngine(_out),
        };

        /// <summary>
        /// The URL to format is null.
        /// </summary>
        [Fact]
        public void NullTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = null;
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(string.Empty);
        }

        /// <summary>
        /// The URL to format is empty.
        /// </summary>
        [Fact]
        public void EmptyTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = string.Empty;
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(t.InputUrl);
        }

        /// <summary>
        /// No InputUrl value is provided. InputUrl is not a required parameter for the task.
        /// </summary>
        [Fact]
        public void NoInputTest()
        {
            var t = GetFormatUrlUnderTest();

            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(string.Empty);
        }

        /// <summary>
        /// The URL to format is white space.
        /// Whitespace is a valid filename character on macOS and Linux, so the task should succeed and
        /// resolve the input against the task's project directory.
        /// </summary>
        [UnixOnlyFact]
        public void WhitespaceTestOnUnix()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = " ";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(new Uri(Path.Combine(Environment.CurrentDirectory, t.InputUrl)).AbsoluteUri);
        }

        /// <summary>
        /// The URL to format is white space.
        /// FormatUrl explicitly fails on Windows for whitespace-only input: it logs a localized
        /// MSB4311 error AND throws <see cref="ArgumentException"/> from <see cref="Path.GetFullPath(string)"/>
        /// to preserve backwards compatibility with any caller relying on the historical exception
        /// contract that was lost when the task migrated to multithreaded execution (relative paths
        /// are now resolved against the project directory via AbsolutePath, which would otherwise
        /// silently trim trailing whitespace and mask the error).
        /// </summary>
        [WindowsOnlyFact]
        public void WhitespaceTestOnWindows()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = " ";
            Should.Throw<ArgumentException>(() => t.Execute());
            ((MockEngine)t.BuildEngine).AssertLogContains("MSB4311");
        }

        /// <summary>
        /// Specifically validates that the Windows-only whitespace guard in <see cref="FormatUrl"/>
        /// fires even when the task is wired up to a real <see cref="TaskEnvironment"/> with an
        /// isolated project directory (i.e. the multithreaded execution path). Without the guard,
        /// the input would silently absolutize against the project directory and lose the
        /// historical <see cref="ArgumentException"/> contract from <see cref="Path.GetFullPath(string)"/>.
        /// Asserts both the localized MSB4311 log entry and the rethrown exception.
        /// </summary>
        [WindowsOnlyFact]
        public void WhitespaceInputFailsOnWindowsWithIsolatedProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_out);
            TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);

            // Sanity check: project directory must differ from the process current directory so we know
            // the guard fires before the AbsolutePath absolutization (which would otherwise silently
            // resolve "projectDir\ " to projectDir on Windows and hide the historical error).
            projectFolder.Path.ShouldNotBe(Environment.CurrentDirectory);

            var engine = new MockEngine(_out);
            var t = new FormatUrl
            {
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectFolder.Path),
                BuildEngine = engine,
                InputUrl = " ",
            };

            Should.Throw<ArgumentException>(() => t.Execute());
            engine.AssertLogContains("MSB4311");
        }

        /// <summary>
        /// The URL to format is a UNC.
        /// </summary>
        [Fact]
        public void UncPathTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = @"\\server\filename.ext";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(@"file://server/filename.ext");
        }

        /// <summary>
        /// The URL to format is a local absolute file path.
        /// This test uses Environment.CurrentDirectory to have a file path value appropriate to the current OS/filesystem.
        /// </summary>
        [Fact]
        public void LocalAbsolutePathTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = Environment.CurrentDirectory;
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(new Uri(t.InputUrl).AbsoluteUri);
        }

        /// <summary>
        /// The URL to format is a local relative file path.
        /// This test uses Environment.CurrentDirectory to have a file path value appropriate to the current OS/filesystem.
        /// </summary>
        [Fact]
        public void LocalRelativePathTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = @".";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(new Uri(Environment.CurrentDirectory).AbsoluteUri);
        }

        /// <summary>
        /// The URL to format is a *nix-style (macOS, Linux) local absolute file path.
        /// </summary>
        [UnixOnlyFact]
        public void LocalUnixAbsolutePathTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = @"/usr/local/share";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(@"file:///usr/local/share");
        }

        /// <summary>
        /// The URL to format is a Windows-style local absolute file path.
        /// </summary>
        [WindowsOnlyFact]
        public void LocalWindowsAbsolutePathTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = @"c:\folder\filename.ext";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(@"file:///c:/folder/filename.ext");
        }

        /// <summary>
        /// The URL to format is a URL using localhost.
        /// </summary>
        [Fact]
        public void UrlLocalHostTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = @"https://localhost/Example/Path";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(@"https://" + Environment.MachineName.ToLowerInvariant() + "/Example/Path");
        }

        /// <summary>
        /// The URL to format is a URL.
        /// </summary>
        [Fact]
        public void UrlTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = @"https://example.com/Example/Path";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(t.InputUrl);
        }

        /// <summary>
        /// The URL to format is a URL with a 'parent' element (..) in the path.
        /// </summary>
        [Fact]
        public void UrlParentPathTest()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = @"https://example.com/Example/../Path";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(@"https://example.com/Path");
        }

        /// <summary>
        /// A relative input URL is resolved against the task's <see cref="TaskEnvironment.ProjectDirectory"/>,
        /// not the process current working directory. This documents the intentional semantic change
        /// introduced when migrating the task to multithreaded execution.
        /// </summary>
        [Fact]
        public void RelativePathResolvesAgainstProjectDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_out);
            TransientTestFolder projectFolder = env.CreateFolder(createFolder: true);

            // Sanity check: project directory must differ from the process current directory
            // for the assertion below to be meaningful.
            projectFolder.Path.ShouldNotBe(Environment.CurrentDirectory);

            var t = new FormatUrl
            {
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectFolder.Path),
                BuildEngine = new MockEngine(_out),
                InputUrl = @".",
            };

            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(new Uri(projectFolder.Path).AbsoluteUri);
        }

        /// <summary>
        /// Two <see cref="FormatUrl"/> instances configured with different
        /// <see cref="TaskEnvironment.ProjectDirectory"/> values resolve the same relative input
        /// independently, each against its own project directory. Locks in the multithreaded-safety
        /// contract: relative-path resolution must derive from the per-task <see cref="TaskEnvironment"/>
        /// rather than any process-wide state (the original bug class motivating the migration).
        /// </summary>
        [Fact]
        public void RelativePathResolvesIndependentlyAcrossInstances()
        {
            using TestEnvironment env = TestEnvironment.Create(_out);
            TransientTestFolder projectFolderA = env.CreateFolder(createFolder: true);
            TransientTestFolder projectFolderB = env.CreateFolder(createFolder: true);

            projectFolderA.Path.ShouldNotBe(projectFolderB.Path);

            var taskA = new FormatUrl
            {
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectFolderA.Path),
                BuildEngine = new MockEngine(_out),
                InputUrl = @".",
            };
            var taskB = new FormatUrl
            {
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectFolderB.Path),
                BuildEngine = new MockEngine(_out),
                InputUrl = @".",
            };

            taskA.Execute().ShouldBeTrue();
            taskB.Execute().ShouldBeTrue();

            taskA.OutputUrl.ShouldBe(new Uri(projectFolderA.Path).AbsoluteUri);
            taskB.OutputUrl.ShouldBe(new Uri(projectFolderB.Path).AbsoluteUri);
        }
    }
}
