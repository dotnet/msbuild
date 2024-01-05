// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests
{
    public sealed class FormatUrl_Tests
    {
        private readonly ITestOutputHelper _out;

        public FormatUrl_Tests(ITestOutputHelper testOutputHelper)
        {
            _out = testOutputHelper;
        }

        /// <summary>
        /// The URL to format is null.
        /// </summary>
        [Fact]
        public void NullTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            t.InputUrl = " ";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(new Uri(Path.Combine(Environment.CurrentDirectory, t.InputUrl)).AbsoluteUri);
        }

        /// <summary>
        /// The URL to format is white space.
        /// </summary>
        [WindowsOnlyFact]
        public void WhitespaceTestOnWindows()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            t.InputUrl = " ";
            Should.Throw<ArgumentException>(() => t.Execute());
        }

        /// <summary>
        /// The URL to format is a UNC.
        /// </summary>
        [Fact]
        public void UncPathTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

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
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            t.InputUrl = @"https://example.com/Example/../Path";
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(@"https://example.com/Path");
        }
    }
}
