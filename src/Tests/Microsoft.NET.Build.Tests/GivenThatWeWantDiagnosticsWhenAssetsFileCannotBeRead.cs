// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

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
            var build = new BuildCommand(testAsset);
            var assetsFile = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            using (var exclusive = File.Open(assetsFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                build.ExecuteWithoutRestore().Should().Fail().And.HaveStdOutContaining(assetsFile);
            }
        }

        [Fact]
        public void It_reports_missing_file()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("HelloWorld").WithSource();
            var build = new BuildCommand(testAsset);
            var assetsFile = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            build.ExecuteWithoutRestore().Should().Fail().And.HaveStdOutContaining(assetsFile);
        }

        [Fact]
        public void It_reports_corrupt_file()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("HelloWorld").WithSource().Restore(Log);
            var build = new BuildCommand(testAsset);
            var assetsFile = Path.Combine(build.GetBaseIntermediateDirectory().FullName, "project.assets.json");

            File.WriteAllText(assetsFile, "{ corrupt_file: ");
            build.ExecuteWithoutRestore().Should().Fail().And.HaveStdOutMatching($"{Regex.Escape(assetsFile)}.*corrupt_file");
        }
    }
}
