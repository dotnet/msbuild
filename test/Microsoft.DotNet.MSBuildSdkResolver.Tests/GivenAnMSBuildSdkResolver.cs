// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Xunit;
using System.Linq;
using Xunit.Abstractions;
using System;
using Microsoft.DotNet.MSBuildSdkResolver;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAnMSBuildSdkResolver
    {
        private ITestOutputHelper _logger;

        public GivenAnMSBuildSdkResolver(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public void ItHasCorrectNameAndPriority()
        {
            var resolver = new DotNetMSBuildSdkResolver();

            Assert.Equal(5000, resolver.Priority);
            Assert.Equal("Microsoft.DotNet.MSBuildSdkResolver", resolver.Name);
        }

        [Fact]
        public void ItCallsNativeCodeWithoutCrashing() // WIP: placeholder to get plumbing through
        {
            var resolver = new DotNetMSBuildSdkResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Microsoft.NET.Sdk", null, null),
                new MockContext(),
                new MockFactory());

            _logger.WriteLine($"success: {result.Success}");
            _logger.WriteLine($"errors: {string.Join(Environment.NewLine, result.Errors ?? Array.Empty<string>())}");
            _logger.WriteLine($"warnings: {string.Join(Environment.NewLine, result.Warnings ?? Array.Empty<string>())}");
            _logger.WriteLine($"path: {result.Path}");
            _logger.WriteLine($"version: {result.Version}");
        }

        private sealed class MockContext : SdkResolverContext
        {
        }

        private sealed class MockFactory : SdkResultFactory
        {
            public override SdkResult IndicateFailure(IEnumerable<string> errors, IEnumerable<string> warnings = null)
                => new MockResult { Success = false, Errors = errors, Warnings = warnings };

            public override SdkResult IndicateSuccess(string path, string version, IEnumerable<string> warnings = null)
                => new MockResult { Success = true, Path = path, Version = version, Warnings = warnings };
        }

        private sealed class MockResult : SdkResult
        {
            public new bool Success { get => base.Success; set => base.Success = value; }
            public string Version { get; set; }
            public string Path { get; set; }
            public IEnumerable<string> Errors { get; set; }
            public IEnumerable<string> Warnings { get; set; }
        }
    }
}
