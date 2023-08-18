// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewDetailsTest : BaseIntegrationTest
    {
        private const string _nuGetPackageId = "Uno.ProjectTemplates.Dotnet";

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/templating/issues/6811")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task CanDisplayDetails_RemotePackage_NuGetFeedWithVersion()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "details", _nuGetPackageId, "--version", "4.8.0-dev.604")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanDisplayDetails_RemotePackage_NuGetFeedNoVersion()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "details", _nuGetPackageId)
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/templating/issues/6811")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task CanDisplayDetails_RemotePackage_OtherFeedWithVersion()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "details", "Microsoft.Azure.WebJobs.ItemTemplates", "--version", "4.0.2288")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Test targeting specific versions")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task CanDisplayDetails_RemotePackage_OtherFeedNoVersion()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "details", "Microsoft.Azure.WebJobs.ItemTemplates")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanDisplayDetails_InstalledPackage_LocalPackage()
        {
            string packageLocation = PackTestNuGetPackage(_log);
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", packageLocation)
                .WithoutBuiltInTemplates()
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            CommandResult commandResult = new DotnetNewCommand(_log, "details", "Microsoft.TemplateEngine.TestTemplates")
                .WithCustomHive(home)
                .WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .UniqueForOSPlatform();
        }

        [Fact]
        public Task CanDisplayDetails_InstalledPackage_NuGetFeed()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", _nuGetPackageId, "--nuget-source", "https://api.nuget.org/v3/index.json")
                .WithoutBuiltInTemplates().WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            CommandResult commandResult = new DotnetNewCommand(_log, "details", _nuGetPackageId)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Test targeting specific versions")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task CanDisplayDetails_InstalledPackage_OtherFeed()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.Azure.WebJobs.ItemTemplates")
                .WithoutBuiltInTemplates().WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            CommandResult commandResult = new DotnetNewCommand(_log, "details", "Microsoft.Azure.WebJobs.ItemTemplates")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanDisplayDetails_InstalledPackage_FolderInstallation()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0);

            CommandResult commandResult = new DotnetNewCommand(_log, "details", basicFSharp)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubAndReplace(basicFSharp, "%TEMPLATE FOLDER%"));
        }
    }
}
