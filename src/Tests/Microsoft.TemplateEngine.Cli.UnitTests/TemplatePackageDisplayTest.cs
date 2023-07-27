// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FakeItEasy;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TemplatePackageDisplayTest
    {
        [Fact]
        public void DisplayUpdateCheckResultTest()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(1, new List<string> { "moderate1", "moderate2" }),
                new VulnerabilityInfo(0, new List<string> { "low1" })
            };
            var updateCheckResult = CheckUpdateResult.CreateFailure(
                A.Fake<IManagedTemplatePackage>(),
                InstallerErrorCode.VulnerablePackage,
                "test message",
                vulnerabilities);
            var expectedErrors =
@"[31m[1mThe package  contains vulnerabilities:[22m[39m

    [1m[34mModerate[39m[22m:
        moderate1
        moderate2

    [1m[36mLow[39m[22m:
        low1".UnixifyLineBreaks();

            packageDisplayer.DisplayUpdateCheckResult(updateCheckResult, A.Fake<ICommandArgs>());

            var reportedErrors = fakeErrorReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            fakeOutputReporter.ReportedStrings.ToString().Should().BeNullOrEmpty();
            reportedErrors.Should().NotBeEmpty();
            Assert.Equal(expectedErrors, reportedErrors);
        }

        [Fact]
        public async Task DisplayInstallResultTest_WithForceSpecified()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(3, new List<string> { "critical1", "critical2", "critical3" }),
                new VulnerabilityInfo(0, new List<string> { "low1" })
            };
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");

            var installResult = InstallResult.CreateSuccess(
                new InstallRequest("PackageA"),
                managedTPA,
                vulnerabilities);
            var expectedOutput =
@"No templates were found in the package PackageA.
Installed package has the following vulnerabilities:

    [1m[31mCritical[39m[22m:
        critical1
        critical2
        critical3

    [1m[36mLow[39m[22m:
        low1".UnixifyLineBreaks();
            ParseResult parseResult = NewCommandFactory.Create("new", _ => CliTestHostFactory.GetVirtualHost()).Parse($"new search foo");

            await packageDisplayer.DisplayInstallResultAsync(
                "PackageA",
                installResult,
                parseResult,
                force: true,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                CancellationToken.None).ConfigureAwait(false);

            var reportedOutput = fakeOutputReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            reportedOutput.Should().NotBeEmpty();
            fakeErrorReporter.ReportedStrings.ToString().Should().BeEmpty();
            Assert.Equal(expectedOutput, reportedOutput);
        }

        [Fact]
        public async Task DisplayInstallResultTest()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(1, new List<string> { "moderate1", "moderate2" }),
                new VulnerabilityInfo(0, new List<string> { "low1" })
            };
            var installResult = InstallResult.CreateFailure(
                new InstallRequest("testPackage"),
                InstallerErrorCode.VulnerablePackage,
                "test message",
                vulnerabilities);
            var expectedErrors =
@"[31m[1mFailed to install testPackage, due to detected vulnerabilities:[22m[39m

    [1m[34mModerate[39m[22m:
        moderate1
        moderate2

    [1m[36mLow[39m[22m:
        low1

[1mIn order to install this package, run:[22m
   new install testPackage --force".UnixifyLineBreaks();

            ParseResult parseResult = NewCommandFactory.Create("new", _ => CliTestHostFactory.GetVirtualHost()).Parse($"new search foo");
            await packageDisplayer.DisplayInstallResultAsync(
                "testPackage",
                installResult,
                parseResult,
                force: false,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                CancellationToken.None).ConfigureAwait(false);

            var reportedErrors = fakeErrorReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            fakeOutputReporter.ReportedStrings.ToString().Should().BeNullOrEmpty();
            reportedErrors.Should().NotBeEmpty();
            Assert.Equal(expectedErrors, reportedErrors);
        }

        [Fact]
        public async Task DisplayInstallResultTest_UpdateRequest()
        {
            var fakeOutputReporter = new FakeReporter();
            var fakeErrorReporter = new FakeReporter();
            var packageDisplayer = new TemplatePackageDisplay(fakeOutputReporter, fakeErrorReporter);
            var vulnerabilities = new List<VulnerabilityInfo>
            {
                new VulnerabilityInfo(2, new List<string> { "high" })
            };
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");
            var updateResult = UpdateResult.CreateFailure(
                new UpdateRequest(managedTPA, "1.0.0"),
                InstallerErrorCode.VulnerablePackage,
                "test message",
                vulnerabilities);
            var expectedErrors =
@"[31m[1mThe package testPackage was not updated due to detected vulnerabilities:[22m[39m

    [1m[33mHigh[39m[22m:
        high

[1mIn order to update this package, run:[22m
   new uninstall testPackage
   new install testPackage --force".UnixifyLineBreaks();

            ParseResult parseResult = NewCommandFactory.Create("new", _ => CliTestHostFactory.GetVirtualHost()).Parse($"new search foo");
            await packageDisplayer.DisplayInstallResultAsync(
                "testPackage",
                updateResult,
                parseResult,
                force: false,
                A.Fake<TemplatePackageManager>(),
                A.Fake<IEngineEnvironmentSettings>(),
                A.Fake<TemplateConstraintManager>(),
                CancellationToken.None).ConfigureAwait(false);

            var reportedErrors = fakeErrorReporter.ReportedStrings.ToString().UnixifyLineBreaks().Trim();
            fakeOutputReporter.ReportedStrings.ToString().Should().BeNullOrEmpty();
            reportedErrors.Should().NotBeEmpty();
            Assert.Equal(expectedErrors, reportedErrors);
        }

        private IManagedTemplatePackage GetFakedManagedTemplatePackage(string mountPointUri, string displayName)
        {
            var managedTemplatePackage = A.Fake<IManagedTemplatePackage>();
            A.CallTo(() => managedTemplatePackage.MountPointUri).Returns(mountPointUri);
            A.CallTo(() => managedTemplatePackage.DisplayName).Returns(displayName);

            return managedTemplatePackage;
        }

        private class FakeReporter : IReporter
        {
            public StringBuilder ReportedStrings { get; set; } = new StringBuilder();

            public void Write(string message) => ReportedStrings.Append(message);

            public void WriteLine(string message) => ReportedStrings.AppendLine(message);

            public void WriteLine() => ReportedStrings.AppendLineN();

            public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));
        }
    }
}
