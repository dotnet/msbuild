// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public sealed class BuildUri_Tests
    {
        public BuildUri_Tests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public void NoInput()
        {
            var task = new BuildUri
            {
                BuildEngine = new MockEngine(true),
            };
            task.Execute().ShouldBeTrue();
            task.OutputUri.ShouldNotBeNull();
            task.OutputUri.Length.ShouldBe(0);
        }

        [Fact]
        public void NoInputUriWithParams()
        {
            var task = new BuildUri
            {
                BuildEngine = new MockEngine(true),
                UriScheme = "https",
                UriPath = "test",
            };
            task.Execute().ShouldBeTrue();
            task.OutputUri.ShouldNotBeNull();
            task.OutputUri.Length.ShouldBe(1);
            task.OutputUri[0].ItemSpec.ShouldBe(@"https://localhost/test");
        }

        [Fact]
        public void EmptyInputUriNoParams()
        {
            var task = new BuildUri
            {
                BuildEngine = new MockEngine(true),
                InputUri = new[] { new TaskItem(string.Empty) },
            };
            task.Execute().ShouldBeTrue();
            task.OutputUri.ShouldNotBeNull();
            task.OutputUri.Length.ShouldBe(0);
        }

        [Fact]
        public void EmptyInputUriWithParams()
        {
            var task = new BuildUri
            {
                BuildEngine = new MockEngine(true),
                InputUri = new[] { new TaskItem(string.Empty) },
                UriScheme = "https",
                UriPath = "test",
            };
            task.Execute().ShouldBeTrue();
            task.OutputUri.ShouldNotBeNull();
            task.OutputUri.Length.ShouldBe(0);
        }

        [Fact]
        public void InputUriNoParams()
        {
            var task = new BuildUri
            {
                BuildEngine = new MockEngine(true),
                InputUri = new[] { new TaskItem(@"https://example.com/test") },
            };
            task.Execute().ShouldBeTrue();
            task.OutputUri.ShouldNotBeNull();
            task.OutputUri.Length.ShouldBe(1);

            var item = task.OutputUri[0];
            item.GetMetadata("UriScheme").ShouldBe(Uri.UriSchemeHttps);
            item.GetMetadata("UriPort").ShouldBe("443");
            item.GetMetadata("UriHost").ShouldBe("example.com");
            item.GetMetadata("UriHostNameType").ShouldBe("Dns");
            item.GetMetadata("UriPath").ShouldBe(@"/test");

            item.GetMetadata("UriUserName").ShouldBe(string.Empty);
            item.GetMetadata("UriPassword").ShouldBe(string.Empty);
            item.GetMetadata("UriQuery").ShouldBe(string.Empty);
            item.GetMetadata("UriFragment").ShouldBe(string.Empty);
        }

        private readonly ITestOutputHelper output;
    }
}
