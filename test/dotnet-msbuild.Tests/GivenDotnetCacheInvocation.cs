// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Cache;
using FluentAssertions;
using Xunit;
using System;
using System.Linq;
using System.IO;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetCacheInvocation
    {
        const string ExpectedPrefix = "exec <msbuildpath> /m /v:m /t:ComposeCache <project>";
        static readonly string[] ArgsPrefix = { "-e", "<project>" };

        [Theory]
        [InlineData("-e")]
        [InlineData("--entries")]
        public void ItAddsProjectToMsbuildInvocation(string optionName)
        {
            var msbuildPath = "<msbuildpath>";
            string[] args = new string[] { optionName, "<project>" };
            CacheCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}");
        }

        [Theory]
        [InlineData(new string[] { "-f", "<tfm>" }, @"/p:TargetFramework=<tfm>")]
        [InlineData(new string[] { "--framework", "<tfm>" }, @"/p:TargetFramework=<tfm>")]
        [InlineData(new string[] { "-r", "<rid>" }, @"/p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "--runtime", "<rid>" }, @"/p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "--entries", "one.xml", "--entries", "two.xml", "--entries", "three.xml" }, @"/p:AdditionalProjects=one.xml%3Btwo.xml%3Bthree.xml")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            args = ArgsPrefix.Concat(args).ToArray();
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            CacheCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
        }

        [Theory]
        [InlineData("-o")]
        [InlineData("--output")]
        public void ItAddsOutputPathToMsBuildInvocation(string optionName)
        {
            string path = Directory.GetCurrentDirectory();
            var args = ArgsPrefix.Concat(new string[] { optionName, path }).ToArray();

            var msbuildPath = "<msbuildpath>";
            CacheCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix} /p:ComposeDir={Path.GetFullPath(path)}");
        }
    }
}
