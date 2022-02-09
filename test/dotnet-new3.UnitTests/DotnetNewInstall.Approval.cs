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
    }
}
