// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using VerifyXunit;
using Xunit;

namespace Dotnet_new3.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewInstallTests
    {
        [Fact]
        public Task CannotInstallPackageAvailableFromBuiltIns()
        {
            var commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verifier.Verify(commandResult.StdErr, _verifySettings)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("   Microsoft\\.DotNet\\.Common\\.ItemTemplates::[A-Za-z0-9.-]+", "   Microsoft.DotNet.Common.ItemTemplates::%VERSION%");
                });
        }

        [Fact]
        public Task CanInstallPackageAvailableFromBuiltInsWithForce()
        {
            var commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100", "--force")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("   Microsoft.DotNet.Common.ItemTemplates::[A-Za-z0-9.-]+", "   Microsoft.DotNet.Common.ItemTemplates::%VERSION%");
                });
        }

        [Fact]
        public Task CannotInstallMultiplePackageAvailableFromBuiltIns()
        {
            var commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100", "Microsoft.DotNet.Web.ItemTemplates::5.0.0")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verifier.Verify(commandResult.StdErr, _verifySettings)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("   Microsoft\\.DotNet\\.Common\\.ItemTemplates::[A-Za-z0-9.-]+", "   Microsoft.DotNet.Common.ItemTemplates::%VERSION%");
                });
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("--install")]
        public Task CanShowDeprecationMessage_WhenLegacyCommandIsUsed(string commandName)
        {
            var commandResult = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ItemTemplates::5.0.0")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix();
        }

        [Fact]
        public Task DoNotShowDeprecationMessage_WhenNewCommandIsUsed()
        {
            var commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Web.ItemTemplates::5.0.0")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verifier.Verify(commandResult.StdOut, _verifySettings);
        }

        [Fact]
        public Task CanShowWarning_WhenConstraintTemplateIsInstalled()
        {
            var testTemplateLocation = TestUtils.GetTestTemplateLocation("Constraints/RestrictedTemplate");
            var commandResult = new DotnetNewCommand(_log, "install", testTemplateLocation)
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Pass();

            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .AddScrubber(output => output.ScrubAndReplace(testTemplateLocation, "%TEMPLATE FOLDER%"));
        }
        
        [Fact]
        public Task CanInstallSameSourceTwice_Folder_WhenSourceIsSpecified()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0);

            var commandResult = new DotnetNewCommand(_log, "install", basicFSharp, "--force")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute();

            commandResult.Should().Pass();
            return Verifier.Verify(commandResult.StdOut, _verifySettings)
                .AddScrubber(output => output.ScrubAndReplace(basicFSharp, "%TEMPLATE FOLDER%"));
        }

        [Fact]
        public Task CanInstallSameSourceTwice_RemoteNuGet_WhenSourceIsSpecified()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0);

            var commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0", "--force")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute();

            commandResult.Should().Pass();
            return Verifier.Verify(commandResult.StdOut, _verifySettings);
        }

        [Fact]
        public Task CannotInstallSameSourceTwice_NuGet()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            var commandResult = new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute();

            commandResult.Should().Fail();
            return Verifier.Verify(commandResult.StdErr, _verifySettings);
        }

        [Fact]
        public Task CannotInstallSameSourceTwice_Folder()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "install", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("basic");

            new DotnetNewCommand(_log, "install", basicFSharp)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().Fail()
                 .And.HaveStdErrContaining($"{basicFSharp} is already installed");

            var commandResult = new DotnetNewCommand(_log, "install", basicFSharp)
           .WithCustomHive(home)
           .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
           .Execute();

            commandResult.Should().Fail();
            return Verifier.Verify(commandResult.StdErr, _verifySettings)
                .AddScrubber(output => output.ScrubAndReplace(basicFSharp, "%TEMPLATE FOLDER%"));
        }

    }
}
