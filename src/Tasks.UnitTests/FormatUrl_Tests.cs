// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

#nullable disable

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
            var expected = string.Empty;

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
        }

        /// <summary>
        /// The URL to format is empty.
        /// </summary>
        [Fact]
        public void EmptyTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            var expected = t.InputUrl = string.Empty;

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
        }

        /// <summary>
        /// The URL to format is white space.
        /// </summary>
        [Fact]
        public void WhitespaceTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            t.InputUrl = " ";
            // From the documentation, Path.GetFullPath(" ") should throw an ArgumentException but it doesn't.
            // If the behavior of Path.GetFullPath(string) changes, this unit test will need to be updated.
            var expected = new Uri(Path.GetFullPath(t.InputUrl)).AbsoluteUri;

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
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
            var expected = new Uri(t.InputUrl).AbsoluteUri;

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
        }

        /// <summary>
        /// The URL to format is a local file path.
        /// </summary>
        [Fact]
        public void LocalPathTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            t.InputUrl = Environment.CurrentDirectory;
            var expected = new Uri(t.InputUrl).AbsoluteUri;

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
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
            uriBuilder.Host = Environment.MachineName.ToLowerInvariant();
            var expected = uriBuilder.ToString();

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
        }

        /// <summary>
        /// The URL to format is a URL.
        /// </summary>
        [Fact]
        public void UrlTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            var uriBuilder = new UriBuilder(Uri.UriSchemeHttps, "example.com") { Path = "Example/Path" };

            var expected = t.InputUrl = uriBuilder.ToString();

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
        }

        /// <summary>
        /// The URL to format is a URL.
        /// </summary>
        [Fact]
        public void UrlParentPathTest()
        {
            var t = new FormatUrl();
            t.BuildEngine = new MockEngine(_out);

            var uriBuilder = new UriBuilder(Uri.UriSchemeHttps, "example.com") { Path = "Example/../Path" };

            t.InputUrl = uriBuilder.ToString();
            var expected = uriBuilder.Uri.AbsoluteUri;

            Assert.True(t.Execute()); // "success"
#if DEBUG
            _out.WriteLine("InputUrl " + ((null == t.InputUrl) ? "is null." : $"= '{t.InputUrl}'."));
            _out.WriteLine("expected " + ((null == expected) ? "is null." : $"= '{expected}'."));
            _out.WriteLine("OutputUrl " + ((null == t.OutputUrl) ? "is null." : $"= '{t.OutputUrl}'."));
#endif
            Assert.Equal(expected, t.OutputUrl);
        }
    }
}