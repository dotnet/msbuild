// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class TelemetryCommandTests : SdkTest
    {
        private readonly FakeRecordEventNameTelemetry _fakeTelemetry;

        public string EventName { get; set; }

        public IDictionary<string, string> Properties { get; set; }

        public TelemetryCommandTests(ITestOutputHelper log) : base(log)
        {
            _fakeTelemetry = new FakeRecordEventNameTelemetry();
            TelemetryEventEntry.Subscribe(_fakeTelemetry.TrackEvent);
            TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
        }

        [Fact]
        public void NoTelemetryIfCommandIsInvalid()
        {
            string[] args = { "publish", "-r" };
            Action a = () => { Cli.Program.ProcessArgs(args); };
            a.Should().NotThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void NoTelemetryIfCommandIsInvalid2()
        {
            string[] args = { "restore", "-v" };
            Action a = () => { Cli.Program.ProcessArgs(args); };
            a.Should().NotThrow<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetry()
        {
            string[] args = { "help" };
            Cli.Program.ProcessArgs(args);

            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("HELP"));
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetryWithPerformanceData()
        {
            string[] args = { "help" };
            Cli.Program.ProcessArgs(args, new TimeSpan(12345));

            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("HELP") &&
                              e.Measurement.ContainsKey("Startup Time") &&
                              e.Measurement["Startup Time"] == 1.2345 &&
                              e.Measurement.ContainsKey("Parse Time") &&
                              e.Measurement["Parse Time"] > 0);
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetryWithoutStartupTime()
        {
            string[] args = { "help" };
            Cli.Program.ProcessArgs(args);

            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("HELP") &&
                              !e.Measurement.ContainsKey("Startup Time") &&
                                 e.Measurement.ContainsKey("Parse Time") &&
                              e.Measurement["Parse Time"] > 0);
        }

        [Fact]
        public void TopLevelCommandNameShouldBeSentToTelemetryZeroStartupTime()
        {
            string[] args = { "help" };
            Cli.Program.ProcessArgs(args, new TimeSpan(0));

            _fakeTelemetry.LogEntries.Should().Contain(e => e.EventName == "toplevelparser/command" &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("HELP") &&
                              !e.Measurement.ContainsKey("Startup Time") &&
                              e.Measurement.ContainsKey("Parse Time") &&
                              e.Measurement["Parse Time"] > 0);
        }

        [Fact]
        public void DotnetNewCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "console";
            string[] args = { "new", argumentToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("NEW"));
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/24190")]
        public void DotnetNewCommandFirstArgumentShouldBeSentToTelemetryWithPerformanceData()
        {
            const string argumentToSend = "console";
            string[] args = { "new", argumentToSend };
            Cli.Program.ProcessArgs(args, new TimeSpan(23456));
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("NEW") &&
                              e.Measurement.ContainsKey("Startup Time") &&
                              e.Measurement["Startup Time"] == 2.3456 &&
                              e.Measurement.ContainsKey("Parse Time") &&
                              e.Measurement["Parse Time"] > 0);
        }

        [Fact]
        public void DotnetHelpCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "something";
            string[] args = { "help", argumentToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("HELP"));
        }

        [Fact]
        public void DotnetAddCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "package";
            string[] args = { "add", argumentToSend, "aPackageName" };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("ADD"));
        }

        [Fact]
        public void DotnetAddCommandFirstArgumentShouldBeSentToTelemetry2()
        {
            const string argumentToSend = "reference";
            string[] args = { "add", argumentToSend, "aPackageName" };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("ADD"));
        }

        [Fact]
        public void DotnetRemoveCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "package";
            string[] args = { "remove", argumentToSend, "aPackageName" };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("REMOVE"));
        }

        [Fact]
        public void DotnetListCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "reference";
            string[] args = { "list", argumentToSend, "aPackageName" };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" && e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("LIST"));
        }

        [Fact]
        public void DotnetSlnCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "list";
            string[] args = { "sln", "aSolution", argumentToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("SLN"));
        }

        [Fact]
        public void DotnetNugetCommandFirstArgumentShouldBeSentToTelemetry()
        {
            const string argumentToSend = "push";

            string[] args = { "nuget", argumentToSend, "path" };

            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey("argument") &&
                              e.Properties["argument"] == Sha256Hasher.Hash(argumentToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("NUGET"));
        }

        [Fact(Skip = "dotnet new sends the telemetry inside own commands")]
        public void DotnetNewCommandLanguageOpinionShouldBeSentToTelemetry()
        {
            const string optionKey = "language";
            const string optionValueToSend = "c#";
            string[] args = { "new", "console", "--" + optionKey, optionValueToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" && e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == Sha256Hasher.Hash(optionValueToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("NEW"));
        }

        [Fact]
        public void AnyDotnetCommandVerbosityOpinionShouldBeSentToTelemetry()
        {
            const string optionKey = "verbosity";
            const string optionValueToSend = "minimal";
            string[] args = { "restore", "--" + optionKey, optionValueToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == Sha256Hasher.Hash(optionValueToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("RESTORE"));
        }

        [Fact]
        public void AnyDotnetCommandVerbosityOpinionShouldBeSentToTelemetryWithPerformanceData()
        {
            const string optionKey = "verbosity";
            const string optionValueToSend = "minimal";
            string[] args = { "restore", "--" + optionKey, optionValueToSend };
            Cli.Program.ProcessArgs(args, new TimeSpan(34567));
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == Sha256Hasher.Hash(optionValueToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("RESTORE") &&
                              e.Measurement.ContainsKey("Startup Time") &&
                              e.Measurement["Startup Time"] == 3.4567 &&
                              e.Measurement.ContainsKey("Parse Time") &&
                              e.Measurement["Parse Time"] > 0);
        }

        [Fact]
        public void DotnetBuildAndPublishCommandOpinionsShouldBeSentToTelemetry()
        {
            const string optionKey = "configuration";
            const string optionValueToSend = "Debug";
            string[] args = { "build", "--" + optionKey, optionValueToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == Sha256Hasher.Hash(optionValueToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("BUILD"));
        }

        [Fact]
        public void DotnetPublishCommandRuntimeOpinionsShouldBeSentToTelemetry()
        {
            const string optionKey = "runtime";
            const string optionValueToSend = $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64";
            string[] args = { "publish", "--" + optionKey, optionValueToSend };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" &&
                              e.Properties.ContainsKey(optionKey) &&
                              e.Properties[optionKey] == Sha256Hasher.Hash(optionValueToSend.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("PUBLISH"));
        }

        [Fact]
        public void DotnetBuildAndPublishCommandOpinionsShouldBeSentToTelemetryWhenThereIsMultipleOption()
        {
            string[] args = { "build", "--configuration", "Debug", "--runtime", $"{ToolsetInfo.LatestMacRuntimeIdentifier}-x64" };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" && e.Properties.ContainsKey("configuration") &&
                              e.Properties["configuration"] == Sha256Hasher.Hash("DEBUG") &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("BUILD"));

            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" && e.Properties.ContainsKey("runtime") &&
                              e.Properties["runtime"] == Sha256Hasher.Hash($"{ToolsetInfo.LatestMacRuntimeIdentifier.ToUpper()}-X64") &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("BUILD"));
        }

        [Fact]
        public void DotnetRunCleanTestCommandOpinionsShouldBeSentToTelemetryWhenThereIsMultipleOption()
        {
            string[] args = { "clean", "--configuration", "Debug", "--framework", ToolsetInfo.CurrentTargetFramework };
            Cli.Program.ProcessArgs(args);
            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" && e.Properties.ContainsKey("configuration") &&
                              e.Properties["configuration"] == Sha256Hasher.Hash("DEBUG") &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("CLEAN"));

            _fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "sublevelparser/command" && e.Properties.ContainsKey("framework") &&
                              e.Properties["framework"] == Sha256Hasher.Hash(ToolsetInfo.CurrentTargetFramework.ToUpper()) &&
                              e.Properties.ContainsKey("verb") &&
                              e.Properties["verb"] == Sha256Hasher.Hash("CLEAN"));
        }

        [WindowsOnlyFact]
        public void InternalreportinstallsuccessCommandCollectExeNameWithEventname()
        {
            FakeRecordEventNameTelemetry fakeTelemetry = new();
            string[] args = { "c:\\mypath\\dotnet-sdk-latest-win-x64.exe" };

            InternalReportinstallsuccess.ProcessInputAndSendTelemetry(args, fakeTelemetry);

            fakeTelemetry
                .LogEntries.Should()
                .Contain(e => e.EventName == "install/reportsuccess" && e.Properties.ContainsKey("exeName") &&
                              e.Properties["exeName"] == Sha256Hasher.Hash("DOTNET-SDK-LATEST-WIN-X64.EXE"));
        }

        [Fact]
        public void ExceptionShouldBeSentToTelemetry()
        {
            Exception caughtException = null;
            try
            {
                string[] args = { "build" };
                Cli.Program.ProcessArgs(args);
                throw new ArgumentException("test exception");
            }
            catch (Exception ex)
            {
                caughtException = ex;
                TelemetryEventEntry.SendFiltered(ex);
            }

            var exception = new Exception();
            _fakeTelemetry
                 .LogEntries.Should()
                 .Contain(e => e.EventName == "mainCatchException/exception" &&
                               e.Properties.ContainsKey("exceptionType") &&
                               e.Properties["exceptionType"] == "System.ArgumentException" &&
                               e.Properties.ContainsKey("detail") &&
                               e.Properties["detail"].Contains(caughtException.StackTrace));
        }
    }
}
