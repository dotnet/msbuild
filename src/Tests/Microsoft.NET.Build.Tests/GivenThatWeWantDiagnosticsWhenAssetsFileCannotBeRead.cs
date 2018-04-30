// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantDiagnosticsWhenAssetsFileCannotBeRead : SdkTest
    {
        public GivenThatWeWantDiagnosticsWhenAssetsFileCannotBeRead(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_reports_inaccessible_file()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("HelloWorld").WithSource().Restore(Log);
            var build = new BuildCommand(Log, testAsset.TestRoot);
            var assetsFile = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            using (var exclusive = File.Open(assetsFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                build.Execute().Should().Fail().And.HaveStdOutContaining(assetsFile);
            }
        }

        [Fact]
        public void It_reports_missing_file()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("HelloWorld").WithSource();
            var build = new BuildCommand(Log, testAsset.TestRoot);
            var assetsFile = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            build.Execute().Should().Fail().And.HaveStdOutContaining(assetsFile);
        }

        [Fact]
        public void It_reports_corrupt_file()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("HelloWorld").WithSource().Restore(Log);
            var build = new BuildCommand(Log, testAsset.TestRoot);
            var assetsFile = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            File.WriteAllText(assetsFile, "{ corrupt_file: ");
            build.Execute().Should().Fail().And.HaveStdOutMatching($"{Regex.Escape(assetsFile)}.*corrupt_file");
        }
    }
}
