// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    public class SourcesTest : SdkTest
    {
        private readonly ITestOutputHelper _log;

        public SourcesTest(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public void EnsureItsPossibleToIncludePackagesLockJson()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("SourceWithExcludeAndWithout", _log, home, workingDirectory);
            new DotnetNewCommand(_log, "withexclude")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0);
            Assert.Equal(
                new[] { "packages.lock.json", "foo.cs", "bar.cs" }.OrderBy(s => s),
                Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories).Select(Path.GetFileName).OrderBy(s => s));

            workingDirectory = TestUtils.CreateTemporaryFolder();
            new DotnetNewCommand(_log, "withoutexclude")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0);
            Assert.Equal(
                new[] { "foo.cs", "bar.cs" }.OrderBy(s => s),
                Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories).Select(Path.GetFileName).OrderBy(s => s));
        }
    }
}
