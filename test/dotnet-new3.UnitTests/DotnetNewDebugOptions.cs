// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    [UsesVerify]
    public class DotnetNewDebugOptions : IClassFixture<VerifySettingsFixture>
    {
        private readonly VerifySettings _verifySettings;
        private readonly ITestOutputHelper _log;

        public DotnetNewDebugOptions(VerifySettingsFixture verifySettings, ITestOutputHelper log)
        {
            _verifySettings = verifySettings.Settings;
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

        [Fact]
        public Task CanShowConfigWithDebugShowConfig()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            var commandResult = new DotnetNewCommand(_log, "--debug:show-config")
               .WithCustomHive(home)
               .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .AddScrubber(output =>
                {
                    string finalOutput = output.ToString();
                    //remove versions
                    output.ScrubByRegex("Version=[A-Za-z0-9\\.]+", "Version=<version>");
                    //remove tokens
                    output.ScrubByRegex("PublicKeyToken=[A-Za-z0-9]+", "PublicKeyToken=<token>");

                    //removes the delimiter line as we don't know the length of last columns containing paths above
                    output.ScrubTableHeaderDelimiter();
                    //removes the spaces after "Assembly" column header as we don't know the amount of spaces after it
                    output.ScrubByRegex("Assembly *", "Assembly");
                });
        }

        [Fact]
        public void DoesNotCreateCacheWhenVirtualHiveIsUsed()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            var envVariable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";

            new DotnetNewCommand(_log, "--debug:ephemeral-hive")
               .WithoutCustomHive()
               .WithEnvironmentVariable(envVariable, home)
               .Execute()
               .Should().Pass().And.NotHaveStdErr();

            Assert.Empty(new DirectoryInfo(home).EnumerateFiles());
        }

        [Fact]
        public void DoesCreateCacheInDifferentLocationWhenCustomHiveIsUsed()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "--debug:custom-hive", home)
               .WithoutCustomHive()
               .Execute()
               .Should().Pass().And.NotHaveStdErr();

            var createdCacheEntries = Directory.GetFileSystemEntries(home);

            Assert.Equal(2, createdCacheEntries.Count());
            Assert.Contains(Path.Combine(home, "packages"), createdCacheEntries);
            Assert.True(File.Exists(Path.Combine(home, "dotnetcli-preview", "v2.0.0", "templatecache.json")));
        }
    }
}
