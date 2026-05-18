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
        /// FormatUrl depends on Path.GetFullPath.
        /// From the documentation, Path.GetFullPath(" ") should throw an ArgumentException, but it doesn't on macOS and Linux
        /// where whitespace characters are valid characters for filenames.
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
        /// PathUtil.Resolve explicitly rejects whitespace-only paths to preserve the historical contract
        /// from <c>Path.GetFullPath(" ")</c>, which threw <see cref="ArgumentException"/> on Windows
        /// before MSBuild's migration from multi-process to multi-threaded execution.
        /// </summary>
        [WindowsOnlyFact]
        public void WhitespaceTestOnWindows()
        {
            var t = GetFormatUrlUnderTest();

            t.InputUrl = " ";
            Should.Throw<ArgumentException>(() => t.Execute());
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
        /// <remarks>
        /// Concrete example. Suppose the process is launched from <c>D:\src\microsoft\dotnet\msbuild</c>
        /// and the engine has loaded a project located in
        /// <c>C:\Users\you\AppData\Local\Temp\msbuild_test_abc123\</c>. In multithreaded mode the engine
        /// hands the task a <see cref="TaskEnvironment"/> whose <c>ProjectDirectory</c> is the project's
        /// folder, *not* the process cwd. Feeding <c>InputUrl = "."</c> must therefore produce
        /// <c>file:///C:/Users/you/AppData/Local/Temp/msbuild_test_abc123/</c> — *not*
        /// <c>file:///D:/src/microsoft/dotnet/msbuild</c>, which is what the multi-process implementation
        /// (rooted in <see cref="System.IO.Path.GetFullPath(string)"/> against the process cwd) would have
        /// produced. The other tests in this file use the Fallback environment where ProjectDirectory == cwd,
        /// so they cannot detect a regression to cwd-based resolution; this test specifically exists to catch that.
        /// </remarks>
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
    }
}
