// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class TelemetryCommandTests : TestBase
    {
        private readonly FakeRecordEventNameTelemetry _fakeTelemetry;

        public string EventName { get; set; }
        public IDictionary<string, string> Properties { get; set; }
        public TelemetryCommandTests()
        {
            _fakeTelemetry = new FakeRecordEventNameTelemetry();
            TelemetryEventEntry.Subscribe(_fakeTelemetry.TrackEvent);
            TelemetryEventEntry.TelemetryFilter = new TelemetryFilter();
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetry()
        {
            string[] args = {"help"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == args[0]);
        }

        [Fact]
        public void DotnetNewCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "console";
            string[] args = {"new", argumentToSend};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-new" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetHelpCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "something";
            string[] args = {"help", argumentToSend};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-help" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetAddCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "package";
            string[] args = {"add", argumentToSend, "aPackageName"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-add" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetAddCommandFirstArgumentShouldBeSentToTelemetry2()
        {
            const string argumentToSend = "reference";
            string[] args = {"add", argumentToSend, "aPackageName"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-add" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetRemoveCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "package";
            string[] args = {"remove", argumentToSend, "aPackageName"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-remove" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetListCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "reference";
            string[] args = {"list", argumentToSend, "aPackageName"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-list" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetSlnCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "list";
            string[] args = {"sln", "aSolution", argumentToSend};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-sln" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetNugetCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "push";
            string[] args = {"nuget", argumentToSend, "aRoot"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-nuget" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == argumentToSend);
        }

        [Fact]
        public void DotnetNewCommandLanguageOpinionShouldBeSentToTelemetry()
        {
            const string optionKey = "language";
            const string optionValueToSend = "c#";
            string[] args = {"new", "console", "--" + optionKey, optionValueToSend};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-new" && e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == optionValueToSend);
        }

        [Fact]
        public void AnyDotnetCommandVerbosityOpinionShouldBeSentToTelemetry()
        {
            const string optionKey = "verbosity";
            const string optionValueToSend = "minimal";
            string[] args = {"restore", "--" + optionKey, optionValueToSend};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-restore" && e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == optionValueToSend);
        }

        [Fact]
        public void DotnetBuildAndPublishCommandOpinionsShouldBeSentToTelemetry()
        {
            const string optionKey = "configuration";
            const string optionValueToSend = "Debug";
            string[] args = {"build", "--" + optionKey, optionValueToSend};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-build" && e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == optionValueToSend);
        }

        [Fact]
        public void DotnetPublishCommandRuntimeOpinionsShouldBeSentToTelemetry()
        {
            const string optionKey = "runtime";
            const string optionValueToSend = "win10-x64";
            string[] args = { "publish", "--" + optionKey, optionValueToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-publish" && e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == optionValueToSend);
        }

        [Fact]
        public void DotnetBuildAndPublishCommandOpinionsShouldBeSentToTelemetryWhenThereIsMultipleOption()
        {
            string[] args = {"build", "--configuration", "Debug", "--runtime", "osx.10.11-x64"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-build" && e.Properties.ContainsKey("configuration") &&
                              e.Properties["configuration"] == "Debug");

            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-build" && e.Properties.ContainsKey("runtime") &&
                              e.Properties["runtime"] == "osx.10.11-x64");
        }

        [Fact]
        public void DotnetRunCleanTestCommandOpinionsShouldBeSentToTelemetryWhenThereIsMultipleOption()
        {
            string[] args = {"clean", "--configuration", "Debug", "--framework", "netcoreapp1.0"};
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-clean" && e.Properties.ContainsKey("configuration") &&
                              e.Properties["configuration"] == "Debug");

            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "dotnet-clean" && e.Properties.ContainsKey("framework") &&
                              e.Properties["framework"] == "netcoreapp1.0");
        }

        [WindowsOnlyFact]
        public void InternalreportinstallsuccessCommandCollectExeNameWithEventname()
        {
            FakeRecordEventNameTelemetry fakeTelemetry = new FakeRecordEventNameTelemetry();
            string[] args = { "c:\\mypath\\dotnet-sdk-latest-win-x64.exe" };

            InternalReportinstallsuccess.ProcessInputAndSendTelemetry(args, fakeTelemetry);

            fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "reportinstallsuccess" && e.Properties.ContainsKey("exeName") &&
                              e.Properties["exeName"] == "dotnet-sdk-latest-win-x64.exe");
        }

        [Fact]
        public void InternalreportinstallsuccessCommandIsRegistedInBuiltIn()
        {
            BuiltInCommandsCatalog.Commands.Should().ContainKey("internal-reportinstallsuccess");
        }
    }
}
