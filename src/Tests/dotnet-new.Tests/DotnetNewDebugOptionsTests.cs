// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public class DotnetNewDebugOptionsTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewDebugOptionsTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public void CanShowBasicInfoWithDebugReinit()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string cacheFilePath = Path.Combine(home, "dotnetcli", Product.Version, "templatecache.json");

            CommandResult commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.True(File.Exists(cacheFilePath));
            DateTime lastUpdateDate = File.GetLastWriteTimeUtc(cacheFilePath);

            CommandResult reinitCommandResult = new DotnetNewCommand(_log, "--debug:reinit")
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
            string home = CreateTemporaryFolder(folderName: "Home");
            string cacheFilePath = Path.Combine(home, "dotnetcli", Product.Version, "templatecache.json");

            CommandResult commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();
            Assert.True(File.Exists(cacheFilePath));
            DateTime lastUpdateDate = File.GetLastWriteTimeUtc(cacheFilePath);

            CommandResult reinitCommandResult = new DotnetNewCommand(_log, "--debug:rebuildcache")
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
            string home = CreateTemporaryFolder(folderName: "Home");
            CommandResult commandResult = new DotnetNewCommand(_log, "--debug:show-config")
               .WithCustomHive(home)
               .Execute();

            commandResult.Should().ExitWith(0).And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform()
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
            string home = CreateTemporaryFolder(folderName: "Home");
            string envVariable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";

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
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "--debug:custom-hive", home)
               .WithoutCustomHive()
               .Execute()
               .Should().Pass().And.NotHaveStdErr();

            string[] createdCacheEntries = Directory.GetFileSystemEntries(home);

            Assert.Equal(2, createdCacheEntries.Length);
            Assert.Contains(Path.Combine(home, "packages"), createdCacheEntries);
            Assert.True(File.Exists(Path.Combine(home, "dotnetcli", Product.Version, "templatecache.json")));
        }

        [Fact]
        public void CanDisableBuiltInTemplates_List()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "list", "--debug:disable-sdk-templates")
                .WithCustomHive(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("console")
                .And.HaveStdOutContaining("No templates installed.");
        }

        [Fact]
        public void CanDisableBuiltInTemplates_Instantiate()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--debug:disable-sdk-templates")
                .WithCustomHive(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.HaveStdErrContaining("No templates or subcommands found matching: 'console'.");
        }
    }
}
