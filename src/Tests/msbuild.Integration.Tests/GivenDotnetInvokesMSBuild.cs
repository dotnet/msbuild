// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.MSBuild.IntegrationTests
{
    public class GivenDotnetInvokesMSBuild : SdkTest
    {
        public GivenDotnetInvokesMSBuild(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("msbuild")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("test")]
        public void When_dotnet_command_invokes_msbuild_Then_env_vars_and_m_are_passed(string command)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildIntegration", identifier: command)
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command)
                .Should().Pass();
        }

        [Theory]
        [InlineData("build")]
        [InlineData("msbuild")]
        [InlineData("pack")]
        [InlineData("publish")]
        public void When_dotnet_command_invokes_msbuild_with_no_args_verbosity_is_set_to_minimum(string command)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildIntegration", identifier: command)
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command);

            cmd.Should().Pass();

            cmd.StdOut
                .Should().NotContain("Message with normal importance", "Because verbosity is set to minimum")
                     .And.Contain("Message with high importance", "Because high importance messages are shown on minimum verbosity");
        }

        [Theory]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("pack")]
        [InlineData("publish")]
        public void When_dotnet_command_invokes_msbuild_with_diag_verbosity_Then_arg_is_passed(string command)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildIntegration", identifier: command)
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command, "-v", "diag");

            cmd.Should().Pass();

            cmd.StdOut.Should().Contain("Message with low importance");
        }

        [Fact]
        public void When_dotnet_test_invokes_msbuild_with_no_args_verbosity_is_set_to_minimum()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildIntegration")
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("test");

            cmd.Should().Pass();
            cmd.StdOut.Should().Contain("Message with high importance");
        }

        [Fact]
        public void When_dotnet_msbuild_command_is_invoked_with_non_msbuild_switch_Then_it_fails()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildIntegration")
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("msbuild", "-v", "diag");

            cmd.ExitCode.Should().NotBe(0);
        }

        [Fact]
        public void When_MSBuildSDKsPath_is_set_by_env_var_then_it_is_not_overridden()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildIntegration")
                .WithSource();

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .WithEnvironmentVariable("MSBuildSDKsPath", "AnyString")
                .Execute($"msbuild");

            cmd.ExitCode.Should().NotBe(0);

            cmd.StdOut.Should().Contain("Expected 'AnyString")
                           .And.Contain("to exist, but it does not.");
        }
    }
}
