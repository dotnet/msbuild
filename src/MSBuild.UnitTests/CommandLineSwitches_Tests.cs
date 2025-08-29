// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class CommandLineSwitchesTests
    {
        public CommandLineSwitchesTests()
        {
            // Make sure resources are initialized
            MSBuildApp.Initialize();
        }

        [Fact]
        public void BogusSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterlessSwitch("bogus", out parameterlessSwitch, out duplicateSwitchErrorMessage).ShouldBeFalse();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.Invalid);
            duplicateSwitchErrorMessage.ShouldBeNull();

            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch("bogus", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeFalse();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Invalid);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeFalse();
        }

        [Theory]
        [InlineData("help")]
        [InlineData("HELP")]
        [InlineData("Help")]
        [InlineData("h")]
        [InlineData("H")]
        [InlineData("?")]
        public void HelpSwitchIdentificationTests(string help)
        {
            CommandLineSwitches.IsParameterlessSwitch(help, out CommandLineSwitches.ParameterlessSwitch parameterlessSwitch, out string duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.Help);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [Theory]
        [InlineData("version")]
        [InlineData("Version")]
        [InlineData("VERSION")]
        [InlineData("ver")]
        [InlineData("VER")]
        [InlineData("Ver")]
        public void VersionSwitchIdentificationTests(string version)
        {
            CommandLineSwitches.IsParameterlessSwitch(version, out CommandLineSwitches.ParameterlessSwitch parameterlessSwitch, out string duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.Version);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [Theory]
        [InlineData("nologo")]
        [InlineData("NOLOGO")]
        [InlineData("NoLogo")]
        public void NoLogoSwitchIdentificationTests(string nologo)
        {
            CommandLineSwitches.IsParameterlessSwitch(nologo, out CommandLineSwitches.ParameterlessSwitch parameterlessSwitch, out string duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.NoLogo);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [Theory]
        [InlineData("noautoresponse")]
        [InlineData("NOAUTORESPONSE")]
        [InlineData("NoAutoResponse")]
        [InlineData("noautorsp")]
        [InlineData("NOAUTORSP")]
        [InlineData("NoAutoRsp")]
        public void NoAutoResponseSwitchIdentificationTests(string noautoresponse)
        {
            CommandLineSwitches.IsParameterlessSwitch(noautoresponse, out CommandLineSwitches.ParameterlessSwitch parameterlessSwitch, out string duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [Theory]
        [InlineData("noconsolelogger")]
        [InlineData("NOCONSOLELOGGER")]
        [InlineData("NoConsoleLogger")]
        [InlineData("noconlog")]
        [InlineData("NOCONLOG")]
        [InlineData("NoConLog")]
        public void NoConsoleLoggerSwitchIdentificationTests(string noconsolelogger)
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterlessSwitch(noconsolelogger, out parameterlessSwitch, out duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [Theory]
        [InlineData("fileLogger")]
        [InlineData("FILELOGGER")]
        [InlineData("FileLogger")]
        [InlineData("fl")]
        [InlineData("FL")]
        public void FileLoggerSwitchIdentificationTests(string filelogger)
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterlessSwitch(filelogger, out parameterlessSwitch, out duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.FileLogger);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [Theory]
        [InlineData("distributedfilelogger")]
        [InlineData("DISTRIBUTEDFILELOGGER")]
        [InlineData("DistributedFileLogger")]
        [InlineData("dfl")]
        [InlineData("DFL")]
        public void DistributedFileLoggerSwitchIdentificationTests(string distributedfilelogger)
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterlessSwitch(distributedfilelogger, out parameterlessSwitch, out duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [Theory]
        [InlineData("ll")]
        [InlineData("LL")]
        [InlineData("livelogger")]
        [InlineData("LiveLogger")]
        [InlineData("LIVELOGGER")]
        [InlineData("tl")]
        [InlineData("TL")]
        [InlineData("terminallogger")]
        [InlineData("TerminalLogger")]
        [InlineData("TERMINALLOGGER")]
        public void TerminalLoggerSwitchIdentificationTests(string terminallogger)
        {
            CommandLineSwitches.ParameterizedSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterizedSwitch(terminallogger, out parameterlessSwitch, out duplicateSwitchErrorMessage, out bool multipleParametersAllowed, out string missingParametersErrorMessage, out bool unquoteParameters, out bool emptyParametersAllowed).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.TerminalLogger);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
            emptyParametersAllowed.ShouldBeTrue();
        }

        [Theory]
        [InlineData("flp")]
        [InlineData("FLP")]
        [InlineData("fileLoggerParameters")]
        [InlineData("FILELOGGERPARAMETERS")]
        public void FileLoggerParametersIdentificationTests(string fileloggerparameters)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(fileloggerparameters, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }

        [Theory]
        [InlineData("tlp")]
        [InlineData("TLP")]
        [InlineData("terminalLoggerParameters")]
        [InlineData("TERMINALLOGGERPARAMETERS")]
        public void TerminalLoggerParametersIdentificationTests(string terminalLoggerParameters)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(terminalLoggerParameters, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.TerminalLoggerParameters);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }

#if FEATURE_NODE_REUSE
        [Theory]
        [InlineData("nr")]
        [InlineData("NR")]
        [InlineData("nodereuse")]
        [InlineData("NodeReuse")]
        public void NodeReuseParametersIdentificationTests(string nodereuse)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(nodereuse, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.NodeReuse);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }
#endif

        [Fact]
        public void ProjectSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch(null, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Project, parameterizedSwitch);
            Assert.NotNull(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            // for the virtual project switch, we match on null, not empty string
            Assert.False(CommandLineSwitches.IsParameterizedSwitch(String.Empty, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Invalid, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.False(unquoteParameters);
        }

        [Theory]
        [InlineData("ignoreprojectextensions")]
        [InlineData("IgnoreProjectExtensions")]
        [InlineData("IGNOREPROJECTEXTENSIONS")]
        [InlineData("ignore")]
        [InlineData("IGNORE")]
        public void IgnoreProjectExtensionsSwitchIdentificationTests(string ignoreprojectextensions)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(ignoreprojectextensions, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }

        [Theory]
        [InlineData("target")]
        [InlineData("TARGET")]
        [InlineData("Target")]
        [InlineData("t")]
        [InlineData("T")]
        public void TargetSwitchIdentificationTests(string target)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(target, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Target);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }

        [Theory]
        [InlineData("property")]
        [InlineData("PROPERTY")]
        [InlineData("Property")]
        [InlineData("p")]
        [InlineData("P")]
        public void PropertySwitchIdentificationTests(string property)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(property, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Property);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }

        [Theory]
        [InlineData("restoreproperty")]
        [InlineData("RESTOREPROPERTY")]
        [InlineData("RestoreProperty")]
        [InlineData("rp")]
        [InlineData("RP")]
        public void RestorePropertySwitchIdentificationTests(string property)
        {
            CommandLineSwitches.IsParameterizedSwitch(property, out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch, out string duplicateSwitchErrorMessage, out bool multipleParametersAllowed, out string missingParametersErrorMessage, out bool unquoteParameters, out bool emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.RestoreProperty);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldBe("MissingPropertyError");
            unquoteParameters.ShouldBeTrue();
        }

        [Theory]
        [InlineData("logger")]
        [InlineData("LOGGER")]
        [InlineData("Logger")]
        [InlineData("l")]
        [InlineData("L")]
        public void LoggerSwitchIdentificationTests(string logger)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(logger, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Logger);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeFalse();
        }

        [Theory]
        [InlineData("verbosity")]
        [InlineData("VERBOSITY")]
        [InlineData("Verbosity")]
        [InlineData("v")]
        [InlineData("V")]
        public void VerbositySwitchIdentificationTests(string verbosity)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(verbosity, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Verbosity);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }

        [Theory]
        [InlineData("ds")]
        [InlineData("DS")]
        [InlineData("Ds")]
        [InlineData("detailedsummary")]
        [InlineData("DETAILEDSUMMARY")]
        [InlineData("DetailedSummary")]
        public void DetailedSummarySwitchIdentificationTests(string detailedsummary)
        {
            CommandLineSwitches.IsParameterizedSwitch(
                detailedsummary,
                out var parameterizedSwitch,
                out var duplicateSwitchErrorMessage,
                out var multipleParametersAllowed,
                out var missingParametersErrorMessage,
                out var unquoteParameters,
                out var emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.DetailedSummary);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBe(false);
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBe(true);
            emptyParametersAllowed.ShouldBe(false);
        }

        [Theory]
        [InlineData("m")]
        [InlineData("M")]
        [InlineData("maxcpucount")]
        [InlineData("MAXCPUCOUNT")]
        public void MaxCPUCountSwitchIdentificationTests(string maxcpucount)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(maxcpucount, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldNotBeNull();
            unquoteParameters.ShouldBeTrue();
        }

#if FEATURE_XML_SCHEMA_VALIDATION
        [Theory]
        [InlineData("validate")]
        [InlineData("VALIDATE")]
        [InlineData("Validate")]
        [InlineData("val")]
        [InlineData("VAL")]
        [InlineData("Val")]
        public void ValidateSwitchIdentificationTests(string validate)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(validate, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Validate);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
        }
#endif

        [Theory]
        [InlineData("preprocess")]
        [InlineData("pp")]
        public void PreprocessSwitchIdentificationTests(string preprocess)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(preprocess, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Preprocess);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
        }

        [Fact]
        public void GetPropertySwitchIdentificationTest()
        {
            CommandLineSwitches.IsParameterizedSwitch(
                "getProperty",
                out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch,
                out string duplicateSwitchErrorMessage,
                out bool multipleParametersAllowed,
                out string missingParametersErrorMessage,
                out _,
                out _);

            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.GetProperty);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void GetItemSwitchIdentificationTest()
        {
            CommandLineSwitches.IsParameterizedSwitch(
                "getItem",
                out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch,
                out string duplicateSwitchErrorMessage,
                out bool multipleParametersAllowed,
                out string missingParametersErrorMessage,
                out _,
                out _);

            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.GetItem);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void GetTargetResultSwitchIdentificationTest()
        {
            CommandLineSwitches.IsParameterizedSwitch(
                "getTargetResult",
                out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch,
                out string duplicateSwitchErrorMessage,
                out bool multipleParametersAllowed,
                out string missingParametersErrorMessage,
                out _,
                out _);

            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.GetTargetResult);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldNotBeNullOrEmpty();
        }

        [Theory]
        [InlineData("targets")]
        [InlineData("tArGeTs")]
        [InlineData("ts")]
        public void TargetsSwitchIdentificationTests(string @switch)
        {
            CommandLineSwitches.IsParameterizedSwitch(
                @switch,
                out var parameterizedSwitch,
                out var duplicateSwitchErrorMessage,
                out var multipleParametersAllowed,
                out var missingParametersErrorMessage,
                out var unquoteParameters,
                out var emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.Targets);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
            emptyParametersAllowed.ShouldBeFalse();
        }

        [Fact]
        public void TargetsSwitchParameter()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            MSBuildApp.GatherCommandLineSwitches(new List<string>() { "/targets:targets.txt" }, switches);

            switches.HaveErrors().ShouldBeFalse();
            switches[CommandLineSwitches.ParameterizedSwitch.Targets].ShouldBe(new[] { "targets.txt" });
        }

        [Fact]
        public void TargetsSwitchDoesNotSupportMultipleOccurrences()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            MSBuildApp.GatherCommandLineSwitches(new List<string>() { "/targets /targets" }, switches);

            switches.HaveErrors().ShouldBeTrue();
        }

        [Theory]
        [InlineData("isolate")]
        [InlineData("ISOLATE")]
        [InlineData("isolateprojects")]
        [InlineData("isolateProjects")]
        public void IsolateProjectsSwitchIdentificationTests(string isolateprojects)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(isolateprojects, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.IsolateProjects);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
            emptyParametersAllowed.ShouldBeFalse();
        }

        [Theory]
        [InlineData("graph")]
        [InlineData("GRAPH")]
        [InlineData("graphbuild")]
        [InlineData("graphBuild")]
        public void GraphBuildSwitchIdentificationTests(string graph)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(graph, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.GraphBuild);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
            emptyParametersAllowed.ShouldBeFalse();
        }

        [Theory]
        [InlineData("low")]
        [InlineData("LOW")]
        [InlineData("lowpriority")]
        [InlineData("lowPriority")]
        public void LowPrioritySwitchIdentificationTests(string lowpriority)
        {
            CommandLineSwitches.IsParameterizedSwitch(lowpriority,
                out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch,
                out string duplicateSwitchErrorMessage,
                out bool multipleParametersAllowed,
                out string missingParametersErrorMessage,
                out bool unquoteParameters,
                out bool emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.LowPriority);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
            emptyParametersAllowed.ShouldBeFalse();
        }

        [Fact]
        public void GraphBuildSwitchCanHaveParameters()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string> { "/graph", "/graph:true;  NoBuild  ;;  ;", "/graph:foo" }, switches);

            switches[CommandLineSwitches.ParameterizedSwitch.GraphBuild].ShouldBe(new[] { "true", "  NoBuild  ", "  ", "foo" });

            switches.HaveErrors().ShouldBeFalse();
        }

        [Fact]
        public void GraphBuildSwitchCanBeParameterless()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string> { "/graph" }, switches);

            switches[CommandLineSwitches.ParameterizedSwitch.GraphBuild].ShouldBe(Array.Empty<string>());

            switches.HaveErrors().ShouldBeFalse();
        }

        [Fact]
        public void InputResultsCachesSupportsMultipleOccurrence()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>() { "/irc", "/irc:a;b", "/irc:c;d" }, switches);

            switches[CommandLineSwitches.ParameterizedSwitch.InputResultsCaches].ShouldBe(new[] { null, "a", "b", "c", "d" });

            switches.HaveErrors().ShouldBeFalse();
        }

        [Fact]
        public void OutputResultsCache()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>() { "/orc:a" }, switches);

            switches[CommandLineSwitches.ParameterizedSwitch.OutputResultsCache].ShouldBe(new[] { "a" });

            switches.HaveErrors().ShouldBeFalse();
        }

        [Fact]
        public void OutputResultsCachesDoesNotSupportMultipleOccurrences()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>() { "/orc:a", "/orc:b" }, switches);

            switches.HaveErrors().ShouldBeTrue();
        }

        [Fact]
        public void SetParameterlessSwitchTests()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoLogo, "/nologo");

            Assert.Equal("/nologo", switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.True(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.True(switches[CommandLineSwitches.ParameterlessSwitch.NoLogo]);

            // set it again
            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoLogo, "-NOLOGO");

            Assert.Equal("-NOLOGO", switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.True(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.True(switches[CommandLineSwitches.ParameterlessSwitch.NoLogo]);

            // we didn't set this switch
            Assert.Null(switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Version));
            Assert.False(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Version));
            Assert.False(switches[CommandLineSwitches.ParameterlessSwitch.Version]);
        }

        [Fact]
        public void SetParameterizedSwitchTests1()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true, false));

            Assert.Equal("/v:q", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Verbosity));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Verbosity));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Verbosity];

            Assert.NotNull(parameters);
            Assert.Single(parameters);
            Assert.Equal("q", parameters[0]);

            // set it again -- this is bogus, because the /verbosity switch doesn't allow multiple parameters, but for the
            // purposes of testing the SetParameterizedSwitch() method, it doesn't matter
            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/verbosity:\"diag\";minimal", "\"diag\";minimal", true, true, false));

            Assert.Equal("/v:q /verbosity:\"diag\";minimal", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Verbosity));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Verbosity));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Verbosity];

            Assert.NotNull(parameters);
            Assert.Equal(3, parameters.Length);
            Assert.Equal("q", parameters[0]);
            Assert.Equal("diag", parameters[1]);
            Assert.Equal("minimal", parameters[2]);
        }

        [Fact]
        public void SetParameterizedSwitchTests2()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            // we haven't set this switch yet
            Assert.Null(switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.False(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.NotNull(parameters);
            Assert.Empty(parameters);

            // fake/missing parameters -- this is bogus because the /target switch allows multiple parameters but we're turning
            // that off here just for testing purposes
            Assert.False(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"", "\"", false, true, false));

            // switch has been set
            Assert.Equal("/t:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            // but no parameters
            Assert.NotNull(parameters);
            Assert.Empty(parameters);

            // more fake/missing parameters
            Assert.False(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:A,\"\";B", "A,\"\";B", true, true, false));

            Assert.Equal("/t:\" /t:A,\"\";B", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            // now we have some parameters
            Assert.NotNull(parameters);
            Assert.Equal(2, parameters.Length);
            Assert.Equal("A", parameters[0]);
            Assert.Equal("B", parameters[1]);
        }

        [Fact]
        public void SetParameterizedSwitchTests3()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            // we haven't set this switch yet
            Assert.Null(switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.False(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.NotNull(parameters);
            Assert.Empty(parameters);

            // don't unquote fake/missing parameters
            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/l:\"", "\"", false, false, false));

            Assert.Equal("/l:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.NotNull(parameters);
            Assert.Single(parameters);
            Assert.Equal("\"", parameters[0]);

            // don't unquote multiple fake/missing parameters -- this is bogus because the /logger switch does not take multiple
            // parameters, but for testing purposes this is fine
            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/LOGGER:\"\",asm;\"p,a;r\"", "\"\",asm;\"p,a;r\"", true, false, false));

            Assert.Equal("/l:\" /LOGGER:\"\",asm;\"p,a;r\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.NotNull(parameters);
            Assert.Equal(4, parameters.Length);
            Assert.Equal("\"", parameters[0]);
            Assert.Equal("\"\"", parameters[1]);
            Assert.Equal("asm", parameters[2]);
            Assert.Equal("\"p,a;r\"", parameters[3]);
        }

        [Fact]
        public void SetParameterizedSwitchTestsAllowEmpty()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.WarningsAsErrors, "/warnaserror", "", multipleParametersAllowed: true, unquoteParameters: false, emptyParametersAllowed: true));

            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.WarningsAsErrors));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.WarningsAsErrors];

            Assert.NotNull(parameters);

            Assert.True(parameters.Length > 0);

            Assert.Null(parameters.Last());
        }

        [Fact]
        public void AppendErrorTests1()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();
            CommandLineSwitches switchesRight = new CommandLineSwitches();

            Assert.False(switchesLeft.HaveErrors());
            Assert.False(switchesRight.HaveErrors());

            switchesLeft.Append(switchesRight);

            Assert.False(switchesLeft.HaveErrors());
            Assert.False(switchesRight.HaveErrors());

            switchesLeft.SetUnknownSwitchError("/bogus");

            Assert.True(switchesLeft.HaveErrors());
            Assert.False(switchesRight.HaveErrors());

            switchesLeft.Append(switchesRight);

            Assert.True(switchesLeft.HaveErrors());
            Assert.False(switchesRight.HaveErrors());

            VerifySwitchError(switchesLeft, "/bogus");

            switchesRight.Append(switchesLeft);

            Assert.True(switchesLeft.HaveErrors());
            Assert.True(switchesRight.HaveErrors());

            VerifySwitchError(switchesLeft, "/bogus");
            VerifySwitchError(switchesRight, "/bogus");
        }

        [Fact]
        public void AppendErrorTests2()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();
            CommandLineSwitches switchesRight = new CommandLineSwitches();

            Assert.False(switchesLeft.HaveErrors());
            Assert.False(switchesRight.HaveErrors());

            switchesLeft.SetUnknownSwitchError("/bogus");
            switchesRight.SetUnexpectedParametersError("/nologo:foo");

            Assert.True(switchesLeft.HaveErrors());
            Assert.True(switchesRight.HaveErrors());

            VerifySwitchError(switchesLeft, "/bogus");
            VerifySwitchError(switchesRight, "/nologo:foo");

            switchesLeft.Append(switchesRight);

            VerifySwitchError(switchesLeft, "/bogus");
            VerifySwitchError(switchesRight, "/nologo:foo");
        }

        [Fact]
        public void AppendParameterlessSwitchesTests()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.Help, "/?");

            Assert.True(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.False(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));

            CommandLineSwitches switchesRight1 = new CommandLineSwitches();

            switchesRight1.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, "/noconlog");

            Assert.False(switchesRight1.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.True(switchesRight1.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));

            switchesLeft.Append(switchesRight1);

            Assert.Equal("/noconlog", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.True(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.True(switchesLeft[CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger]);

            // this switch is not affected
            Assert.Equal("/?", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.True(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.True(switchesLeft[CommandLineSwitches.ParameterlessSwitch.Help]);

            CommandLineSwitches switchesRight2 = new CommandLineSwitches();

            switchesRight2.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, "/NOCONSOLELOGGER");

            Assert.False(switchesRight2.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.True(switchesRight2.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));

            switchesLeft.Append(switchesRight2);

            Assert.Equal("/NOCONSOLELOGGER", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.True(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.True(switchesLeft[CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger]);

            Assert.Equal("/?", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.True(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.True(switchesLeft[CommandLineSwitches.ParameterlessSwitch.Help]);

            Assert.False(switchesLeft.HaveErrors());
        }

        [Fact]
        public void AppendParameterizedSwitchesTests1()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true, false);

            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.False(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:build", "build", true, true, false);

            Assert.False(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.True(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            switchesLeft.Append(switchesRight);

            Assert.Equal("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.NotNull(parameters);
            Assert.Single(parameters);
            Assert.Equal("tempproject.proj", parameters[0]);

            Assert.Equal("/t:build", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.NotNull(parameters);
            Assert.Single(parameters);
            Assert.Equal("build", parameters[0]);
        }

        [Fact]
        public void AppendParameterizedSwitchesTests2()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/target:Clean", "Clean", true, true, false);

            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"RESOURCES\";build", "\"RESOURCES\";build", true, true, false);

            Assert.True(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            switchesLeft.Append(switchesRight);

            Assert.Equal("/t:\"RESOURCES\";build", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.NotNull(parameters);
            Assert.Equal(3, parameters.Length);
            Assert.Equal("Clean", parameters[0]);
            Assert.Equal("RESOURCES", parameters[1]);
            Assert.Equal("build", parameters[2]);
        }

        [Fact]
        public void AppendParameterizedSwitchesTests3()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true, false);

            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "Rhubarb.proj", "Rhubarb.proj", false, true, false);

            Assert.True(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            switchesLeft.Append(switchesRight);

            Assert.Equal("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.NotNull(parameters);
            Assert.Single(parameters);
            Assert.Equal("tempproject.proj", parameters[0]);

            Assert.True(switchesLeft.HaveErrors());

            VerifySwitchError(switchesLeft, "Rhubarb.proj");
        }

        [Fact]
        public void InvalidToolsVersionErrors()
        {
            Assert.Throws<InitializationException>(() =>
            {
                string filename = null;
                try
                {
                    filename = FileUtilities.GetTemporaryFileName();
                    ProjectRootElement project = ProjectRootElement.Create();
                    project.Save(filename);
                    BuildResult buildResult = null;
                    MSBuildApp.BuildProject(
                                        filename,
                                        null,
                                        "ScoobyDoo",
                                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                                        Array.Empty<ILogger>(),
                                        LoggerVerbosity.Normal,
                                        Array.Empty<DistributedLoggerRecord>(),
#if FEATURE_XML_SCHEMA_VALIDATION
                                        false,
                                        null,
#endif
                                        1,
                                        true,
                                        new StringWriter(),
                                        new StringWriter(),
                                        false,
                                        warningsAsErrors: null,
                                        warningsNotAsErrors: null,
                                        warningsAsMessages: null,
                                        enableRestore: false,
                                        profilerLogger: null,
                                        enableProfiler: false,
                                        interactive: false,
                                        isolateProjects: ProjectIsolationMode.False,
                                        graphBuildOptions: null,
                                        lowPriority: false,
                                        question: false,
                                        inputResultsCaches: null,
                                        outputResultsCache: null,
                                        saveProjectResult: false,
                                        ref buildResult,
#if FEATURE_REPORTFILEACCESSES
                                        reportFileAccesses: false,
#endif
                                        commandLine: null);
                }
                finally
                {
                    if (File.Exists(filename))
                    {
                        File.Delete(filename);
                    }
                }
            });
        }
        [Fact]
        public void TestHaveAnySwitchesBeenSet()
        {
            // Check if method works with parameterized switch
            CommandLineSwitches switches = new CommandLineSwitches();
            Assert.False(switches.HaveAnySwitchesBeenSet());
            switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true, false);
            Assert.True(switches.HaveAnySwitchesBeenSet());

            // Check if method works with parameterless switches
            switches = new CommandLineSwitches();
            Assert.False(switches.HaveAnySwitchesBeenSet());
            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.Help, "/?");
            Assert.True(switches.HaveAnySwitchesBeenSet());
        }

#if FEATURE_NODE_REUSE
        /// <summary>
        /// /nodereuse:false /nodereuse:true should result in "true"
        /// </summary>
        [Fact]
        public void ProcessNodeReuseSwitchTrueLast()
        {
            bool nodeReuse = MSBuildApp.ProcessNodeReuseSwitch(new string[] { "false", "true" });

            Assert.True(nodeReuse);
        }

        /// <summary>
        /// /nodereuse:true /nodereuse:false should result in "false"
        /// </summary>
        [Fact]
        public void ProcessNodeReuseSwitchFalseLast()
        {
            bool nodeReuse = MSBuildApp.ProcessNodeReuseSwitch(new string[] { "true", "false" });

            Assert.False(nodeReuse);
        }
#endif

        /// <summary>
        /// Regress DDB #143341:
        ///     msbuild /clp:v=quiet /clp:v=diag /m:2
        /// gave console logger in quiet verbosity; expected diagnostic
        /// </summary>
        [Fact]
        public void ExtractAnyLoggerParameterPickLast()
        {
            string result = MSBuildApp.ExtractAnyLoggerParameter("v=diag;v=q", new string[] { "v", "verbosity" });

            Assert.Equal("v=q", result);
        }

        /// <summary>
        /// Verifies that when the /warnaserror switch is not specified, the set of warnings is null.
        /// </summary>
        [Fact]
        public void ProcessWarnAsErrorSwitchNotSpecified()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[] { "" }), commandLineSwitches);

            Assert.Null(MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches));
        }

        /// <summary>
        /// Verifies that the /warnaserror switch is parsed properly when codes are specified.
        /// </summary>
        [Fact]
        public void ProcessWarnAsErrorSwitchWithCodes()
        {
            ISet<string> expectedWarningsAsErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "B", "c", "D", "e" };

            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[]
            {
                "\"/warnaserror: a,B ; c \"", // Leading, trailing, leading and trailing whitespace
                "/warnaserror:A,b,C",         // Repeats of different case
                "\"/warnaserror:,    ,,\"",   // Empty items
                "/err:D,d;E,e",               // A different source with new items and uses the short form
                "/warnaserror:a",             // A different source with a single duplicate
                "/warnaserror:a,b",           // A different source with  multiple duplicates
            }), commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.NotNull(actualWarningsAsErrors);

            Assert.Equal(expectedWarningsAsErrors, actualWarningsAsErrors, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that an empty /warnaserror switch clears the list of codes.
        /// </summary>
        [Fact]
        public void ProcessWarnAsErrorSwitchEmptySwitchClearsSet()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[]
            {
                "/warnaserror:a;b;c",
                "/warnaserror",
            }), commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.NotNull(actualWarningsAsErrors);

            Assert.Empty(actualWarningsAsErrors);
        }

        /// <summary>
        /// Verifies that when values are specified after an empty /warnaserror switch that they are added to the cleared list.
        /// </summary>
        [Fact]
        public void ProcessWarnAsErrorSwitchValuesAfterEmptyAddOn()
        {
            ISet<string> expectedWarningsAsErors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "e", "f", "g" };

            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[]
            {
                "/warnaserror:a;b;c",
                "/warnaserror",
                "/warnaserror:e;f;g",
            }), commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.NotNull(actualWarningsAsErrors);

            Assert.Equal(expectedWarningsAsErors, actualWarningsAsErrors, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that the /warnaserror switch is parsed properly when no codes are specified.
        /// </summary>
        [Fact]
        public void ProcessWarnAsErrorSwitchEmpty()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[] { "/warnaserror" }), commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.NotNull(actualWarningsAsErrors);

            Assert.Empty(actualWarningsAsErrors);
        }

        /// <summary>
        /// Verifies that when the /warnasmessage switch is used with no values that an error is shown.
        /// </summary>
        [Fact]
        public void ProcessWarnAsMessageSwitchEmpty()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            // Set "expanded" content to match the placeholder so the verify can use the exact resource string as "expected."
            string command = "{0}";
            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[] { "/warnasmessage" }), commandLineSwitches, command);

            VerifySwitchError(commandLineSwitches, "/warnasmessage", AssemblyResources.GetString("MissingWarnAsMessageParameterError"));
        }

        /// <summary>
        /// Verify that environment variables cannot be passed in as command line switches.
        /// Also verifies that the full command line is properly passed when a switch error occurs.
        /// </summary>
        [Fact]
        public void ProcessEnvironmentVariableSwitch()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("ENVIRONMENTVARIABLE", string.Empty);

                CommandLineSwitches commandLineSwitches = new();
                string fullCommandLine = "msbuild validProject.csproj %ENVIRONMENTVARIABLE%";
                MSBuildApp.GatherCommandLineSwitches(new List<string>() { "validProject.csproj", "%ENVIRONMENTVARIABLE%" }, commandLineSwitches, fullCommandLine);
                VerifySwitchError(commandLineSwitches, "%ENVIRONMENTVARIABLE%", String.Format(AssemblyResources.GetString("EnvironmentVariableAsSwitch"), fullCommandLine));

                commandLineSwitches = new();
                fullCommandLine = "msbuild %ENVIRONMENTVARIABLE% validProject.csproj";
                MSBuildApp.GatherCommandLineSwitches(new List<string>() { "%ENVIRONMENTVARIABLE%", "validProject.csproj" }, commandLineSwitches, fullCommandLine);
                VerifySwitchError(commandLineSwitches, "%ENVIRONMENTVARIABLE%", String.Format(AssemblyResources.GetString("EnvironmentVariableAsSwitch"), fullCommandLine));
            }
        }

        /// <summary>
        /// Verifies that the /warnasmessage switch is parsed properly when codes are specified.
        /// </summary>
        [Fact]
        public void ProcessWarnAsMessageSwitchWithCodes()
        {
            ISet<string> expectedWarningsAsMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "B", "c", "D", "e" };

            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[]
            {
                "\"/warnasmessage: a,B ; c \"", // Leading, trailing, leading and trailing whitespace
                "/warnasmessage:A,b,C",         // Repeats of different case
                "\"/warnasmessage:,    ,,\"",   // Empty items
                "/nowarn:D,d;E,e",              // A different source with new items and uses the short form
                "/warnasmessage:a",             // A different source with a single duplicate
                "/warnasmessage:a,b",           // A different source with  multiple duplicates
            }), commandLineSwitches);

            ISet<string> actualWarningsAsMessages = MSBuildApp.ProcessWarnAsMessageSwitch(commandLineSwitches);

            Assert.NotNull(actualWarningsAsMessages);

            Assert.Equal(expectedWarningsAsMessages, actualWarningsAsMessages, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that when the /profileevaluation switch is used with no values "no-file" is specified.
        /// </summary>
        [Fact]
        public void ProcessProfileEvaluationEmpty()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();

            MSBuildApp.GatherCommandLineSwitches(new List<string>(new[] { "/profileevaluation" }), commandLineSwitches);
            commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.ProfileEvaluation][0].ShouldBe("no-file");
        }

        [Fact]
        public void ProcessBooleanSwitchTest()
        {
            MSBuildApp.ProcessBooleanSwitch(Array.Empty<string>(), defaultValue: true, resourceName: null).ShouldBeTrue();

            MSBuildApp.ProcessBooleanSwitch(Array.Empty<string>(), defaultValue: false, resourceName: null).ShouldBeFalse();

            MSBuildApp.ProcessBooleanSwitch(new[] { "true" }, defaultValue: false, resourceName: null).ShouldBeTrue();

            MSBuildApp.ProcessBooleanSwitch(new[] { "false" }, defaultValue: true, resourceName: null).ShouldBeFalse();

            Should.Throw<CommandLineSwitchException>(() => MSBuildApp.ProcessBooleanSwitch(new[] { "invalid" }, defaultValue: true, resourceName: "InvalidRestoreValue"));
        }

        public static IEnumerable<object[]> ProcessGraphBuildSwitchData()
        {
            var emptyOptions = new GraphBuildOptions();
            var noBuildOptions = new GraphBuildOptions { Build = false };

            yield return new object[] { Array.Empty<string>(), emptyOptions, null };

            yield return new object[] { new[] { "true" }, emptyOptions, null };

            yield return new object[] { new[] { "false" }, null, null };

            yield return new object[] { new[] { "  ", "  " }, emptyOptions, null };

            yield return new object[] { new[] { "NoBuild" }, noBuildOptions, null };

            yield return new object[] { new[] { "noBUILD" }, noBuildOptions, null };

            yield return new object[] { new[] { "noBUILD     " }, noBuildOptions, null };

            yield return new object[] { new[] { "false", "true" }, null, new[] { "false" } };

            yield return new object[] { new[] { "nobuild", "true" }, noBuildOptions, new[] { "true" } };

            yield return new object[] { new[] { "false", "nobuild" }, null, new[] { "false" } };

            yield return new object[] { new[] { "nobuild", "invalid" }, null, new[] { "invalid" } };
        }

        [Theory]
        [MemberData(nameof(ProcessGraphBuildSwitchData))]
        public void ProcessGraphBuildSwitch(string[] parameters, GraphBuildOptions expectedOptions, string[] expectedWordsInException)
        {
            CommandLineSwitchException exception = null;

            try
            {
                var graphBuildOptions = MSBuildApp.ProcessGraphBuildSwitch(parameters);
                graphBuildOptions.ShouldBe(expectedOptions);
            }
            catch (CommandLineSwitchException e)
            {
                exception = e;
            }

            if (expectedWordsInException != null)
            {
                exception.ShouldNotBeNull();

                exception.Message.ShouldContain("Graph build value is not valid");

                foreach (var expectedWord in expectedWordsInException)
                {
                    exception.Message.ShouldContain(expectedWord);
                }
            }
            else
            {
                exception.ShouldBeNull();
            }
        }

        /// <summary>
        /// Verifies that the /target switch is parsed properly with invalid characters.
        /// </summary>
        [Fact]
        public void ProcessInvalidTargetSwitch()
        {
            string projectContent = """
                <Project>
                </Project>
                """;
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            string project = testEnvironment.CreateTestProjectWithFiles("project.proj", projectContent).ProjectFile;

#if FEATURE_GET_COMMANDLINE
            MSBuildApp.Execute(@"msbuild.exe " + project + " /t:foo.bar").ShouldBe(MSBuildApp.ExitType.SwitchError);
#else
            MSBuildApp.Execute(new[] { @"msbuild.exe", project, "/t:foo.bar" }).ShouldBe(MSBuildApp.ExitType.SwitchError);
#endif
        }

        /// <summary>
        /// Verifies that when the /profileevaluation switch is used with invalid filenames an error is shown.
        /// </summary>
        [MemberData(nameof(GetInvalidFilenames))]
        [WindowsFullFrameworkOnlyTheory(additionalMessage: ".NET Core 2.1+ no longer validates paths: https://github.com/dotnet/corefx/issues/27779#issuecomment-371253486.")]
        public void ProcessProfileEvaluationInvalidFilename(string filename)
        {
            bool enableProfiler = false;
            Should.Throw(
                () => MSBuildApp.ProcessProfileEvaluationSwitch(new[] { filename }, new List<ILogger>(), out enableProfiler),
                typeof(CommandLineSwitchException));
        }

        public static IEnumerable<object[]> GetInvalidFilenames()
        {
            yield return new object[] { $"a_file_with${Path.GetInvalidFileNameChars().First()}invalid_chars" };
            yield return new object[] { $"C:\\a_path\\with{Path.GetInvalidPathChars().First()}invalid\\chars" };
        }

        /// <summary>
        /// Verifies that help messages are correctly formed with the right width and leading spaces.
        /// </summary>
        [Fact]
        public void HelpMessagesAreValid()
        {
            ResourceManager resourceManager = new ResourceManager("MSBuild.Strings", typeof(AssemblyResources).Assembly);

            const string switchLeadingSpaces = "  ";
            const string otherLineLeadingSpaces = "                     ";
            const string examplesLeadingSpaces = "        ";

            foreach (KeyValuePair<string, string> item in resourceManager.GetResourceSet(CultureInfo.CurrentUICulture, createIfNotExists: true, tryParents: true)
                .Cast<DictionaryEntry>().Where(i => i.Key is string && ((string)i.Key).StartsWith("HelpMessage_"))
                .Select(i => new KeyValuePair<string, string>((string)i.Key, (string)i.Value)))
            {
                string[] helpMessageLines = item.Value.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < helpMessageLines.Length; i++)
                {
                    // All lines should be 80 characters or less
                    Assert.True(helpMessageLines[i].Length <= 80, $"Line {i + 1} of '{item.Key}' should be no longer than 80 characters.");

                    string trimmedLine = helpMessageLines[i].Trim();

                    if (i == 0)
                    {
                        if (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("@"))
                        {
                            // If the first line in a switch it needs a certain amount of leading spaces
                            Assert.StartsWith(switchLeadingSpaces, helpMessageLines[i]);
                        }
                        else
                        {
                            // Otherwise it should have no leading spaces because it's a section
                            Assert.False(helpMessageLines[i].StartsWith(" "));
                        }
                    }
                    else
                    {
                        // Ignore empty lines
                        if (!String.IsNullOrWhiteSpace(helpMessageLines[i]))
                        {
                            if (item.Key.Contains("Examples"))
                            {
                                // Examples require a certain number of leading spaces
                                Assert.StartsWith(examplesLeadingSpaces, helpMessageLines[i]);
                            }
                            else if (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("@"))
                            {
                                // Switches require a certain number of leading spaces
                                Assert.StartsWith(switchLeadingSpaces, helpMessageLines[i]);
                            }
                            else
                            {
                                // All other lines require a certain number of leading spaces
                                Assert.StartsWith(otherLineLeadingSpaces, helpMessageLines[i]);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that a switch collection has an error registered for the given command line arg.
        /// </summary>
        private void VerifySwitchError(CommandLineSwitches switches, string badCommandLineArg, string expectedMessage = null)
        {
            bool caughtError = false;

            try
            {
                switches.ThrowErrors();
            }
            catch (CommandLineSwitchException e)
            {
                Assert.Equal(badCommandLineArg, e.CommandLineArg);

                caughtError = true;

                if (expectedMessage != null)
                {
                    Assert.Contains(expectedMessage, e.Message);
                }

                // so I can see the message in NUnit's "Standard Out" window
                Console.WriteLine(e.Message);
            }
            finally
            {
                Assert.True(caughtError);
            }
        }
    }
}
