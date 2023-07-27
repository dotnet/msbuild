// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewDetailsTest : BaseIntegrationTest, IClassFixture<DiagnosticFixture>
    {
        private readonly ITestOutputHelper _log;
        private readonly IMessageSink _messageSink;

        public DotnetNewDetailsTest(DiagnosticFixture diagnosisFixture, ITestOutputHelper log) : base(log)
        {
            _log = log;
            _messageSink = diagnosisFixture.DiagnosticSink;
        }

        [Fact]
        public void CanDisplayDetails_LocalPackage()
        {
            string packageLocation = PackTestNuGetPackage(_log);
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", packageLocation)
                .WithoutBuiltInTemplates().WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, "details", "Microsoft.TemplateEngine.TestTemplates")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching($"Microsoft.TemplateEngine.TestTemplates{Environment.NewLine}   Authors:{Environment.NewLine}      Microsoft{Environment.NewLine}   Templates:");
        }

        [Fact]
        public void CannotDisplayUnknownPackageDetails()
        {
            new DotnetNewCommand(_log, "details", "Some package that does not exist")
            .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(103)
                .And.HaveStdErr()
                .And.HaveStdOutMatching("No template packages found matching: Some package that does not exist.");
        }
    }
}
