// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    sealed public class FormatUrl_Tests
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
        /// The URL to format is white space.
        /// FormatUrl depends on Path.GetFullPath.
        /// From the documentation, Path.GetFullPath(" ") should throw an ArgumentException, but it doesn't on macOS and Linux
        /// where whitespace characters are valid characters for filenames.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.AnyUnix)]
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
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
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
        /// The URL to format is a local file path.
        /// This test uses Environment.CurrentDirectory to have a file path value appropriate to the current OS/filesystem. 
        /// </summary>
        [Fact]
        public void LocalPathTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            t.InputUrl = Environment.CurrentDirectory;
            t.Execute().ShouldBeTrue();
            t.OutputUrl.ShouldBe(new Uri(t.InputUrl).AbsoluteUri);
        }

        /// <summary>
        /// The URL to format is a URL using localhost.
        /// </summary>
        [Fact]
        public void UrlLocalHostTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            var uriBuilder = new UriBuilder(Uri.UriSchemeHttps, "localhost") { Path = "Example/Path" };
            t.InputUrl = uriBuilder.ToString();
            t.Execute().ShouldBeTrue();
            uriBuilder.Host = Environment.MachineName.ToLowerInvariant();
            t.OutputUrl.ShouldBe(uriBuilder.ToString());
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
        /// The URL to format is a URL.
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