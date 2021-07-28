// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewDebugOptions
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewDebugOptions(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanShowBasicInfoWithDebugReinit()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string cacheFilePath = Path.Combine(home, "dotnetcli-preview", "v2.0.0", "templatecache.json");

            var commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.True(File.Exists(cacheFilePath));
            DateTime lastUpdateDate = File.GetLastWriteTimeUtc(cacheFilePath);

            var reinitCommandResult = new DotnetNewCommand(_log, "--debug:reinit")
               .WithCustomHive(home)
               .Execute();

            reinitCommandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.Equal(commandResult.StdOut, reinitCommandResult.StdOut);
            Assert.True(File.Exists(cacheFilePath));
            Assert.True(lastUpdateDate < File.GetLastWriteTimeUtc(cacheFilePath));
        }

        [Fact]
        public void CanShowBasicInfoWithDebugRebuildCache()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string cacheFilePath = Path.Combine(home, "dotnetcli-preview", "v2.0.0", "templatecache.json");

            var commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.True(File.Exists(cacheFilePath));
            DateTime lastUpdateDate = File.GetLastWriteTimeUtc(cacheFilePath);

            var reinitCommandResult = new DotnetNewCommand(_log, "--debug:rebuildcache")
               .WithCustomHive(home)
               .Execute();

            reinitCommandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.Equal(commandResult.StdOut, reinitCommandResult.StdOut);
            Assert.True(File.Exists(cacheFilePath));
            Assert.True(lastUpdateDate < File.GetLastWriteTimeUtc(cacheFilePath));
        }
    }
}
