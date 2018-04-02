// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Store;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetStoreInvocation
    {
        const string ExpectedPrefix = "exec <msbuildpath> -m -v:m /t:ComposeStore <project>";
        static readonly string[] ArgsPrefix = { "-m", "<project>" };

        [Theory]
        [InlineData("-m")]
        [InlineData("--manifest")]
        public void ItAddsProjectToMsbuildInvocation(string optionName)
        {
            var msbuildPath = "<msbuildpath>";
            string[] args = new string[] { optionName, "<project>" };
            StoreCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}");
        }

        [Theory]
        [InlineData(new string[] { "-f", "<tfm>" }, @"/p:TargetFramework=<tfm>")]
        [InlineData(new string[] { "--framework", "<tfm>" }, @"/p:TargetFramework=<tfm>")]
        [InlineData(new string[] { "-r", "<rid>" }, @"/p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "--runtime", "<rid>" }, @"/p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "--manifest", "one.xml", "--manifest", "two.xml", "--manifest", "three.xml" }, @"/p:AdditionalProjects=one.xml%3Btwo.xml%3Bthree.xml")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            args = ArgsPrefix.Concat(args).ToArray();
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            StoreCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
        }

        [Theory]
        [InlineData("-o")]
        [InlineData("--output")]
        public void ItAddsOutputPathToMsBuildInvocation(string optionName)
        {
            string path = "/some/path";
            var args = ArgsPrefix.Concat(new string[] { optionName, path }).ToArray();

            var msbuildPath = "<msbuildpath>";
            StoreCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix} /p:ComposeDir={Path.GetFullPath(path)}");
        }
    }
}
