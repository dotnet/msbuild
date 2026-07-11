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
using Microsoft.Build.CommandLine.Experimental;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Shared;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class CommandLineSwitchesTests
    {
        public CommandLineSwitchesTests()
        {
            // Make sure resources are initialized
            MSBuildApp.Initialize();
            // Reset this static member that might be changed in some tests to avoid side effects.
            CommandLineSwitches.SwitchesFromResponseFiles = new();
        }

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        [DataRow("help")]
        [DataRow("HELP")]
        [DataRow("Help")]
        [DataRow("h")]
        [DataRow("H")]
        [DataRow("?")]
        public void HelpSwitchIdentificationTests(string help)
        {
            CommandLineSwitches.IsParameterlessSwitch(help, out CommandLineSwitches.ParameterlessSwitch parameterlessSwitch, out string duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.Help);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [MSBuildTestMethod]
        [DataRow("version")]
        [DataRow("Version")]
        [DataRow("VERSION")]
        [DataRow("ver")]
        [DataRow("VER")]
        [DataRow("Ver")]
        public void VersionSwitchIdentificationTests(string version)
        {
            CommandLineSwitches.IsParameterlessSwitch(version, out CommandLineSwitches.ParameterlessSwitch parameterlessSwitch, out string duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.Version);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [MSBuildTestMethod]
        [DataRow("nologo")]
        [DataRow("NOLOGO")]
        [DataRow("NoLogo")]
        public void NoLogoSwitchIdentificationTests(string nologo)
        {
            CommandLineSwitches.IsParameterizedSwitch(nologo, out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch, out string duplicateSwitchErrorMessage, out bool multipleParametersAllowed, out string missingParametersErrorMessage, out bool unquoteParameters, out bool emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.NoLogo);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [MSBuildTestMethod]
        [DataRow("noautoresponse")]
        [DataRow("NOAUTORESPONSE")]
        [DataRow("NoAutoResponse")]
        [DataRow("noautorsp")]
        [DataRow("NOAUTORSP")]
        [DataRow("NoAutoRsp")]
        public void NoAutoResponseSwitchIdentificationTests(string noautoresponse)
        {
            CommandLineSwitches.IsParameterlessSwitch(noautoresponse, out CommandLineSwitches.ParameterlessSwitch parameterlessSwitch, out string duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [MSBuildTestMethod]
        [DataRow("noconsolelogger")]
        [DataRow("NOCONSOLELOGGER")]
        [DataRow("NoConsoleLogger")]
        [DataRow("noconlog")]
        [DataRow("NOCONLOG")]
        [DataRow("NoConLog")]
        public void NoConsoleLoggerSwitchIdentificationTests(string noconsolelogger)
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterlessSwitch(noconsolelogger, out parameterlessSwitch, out duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [MSBuildTestMethod]
        [DataRow("fileLogger")]
        [DataRow("FILELOGGER")]
        [DataRow("FileLogger")]
        [DataRow("fl")]
        [DataRow("FL")]
        public void FileLoggerSwitchIdentificationTests(string filelogger)
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterlessSwitch(filelogger, out parameterlessSwitch, out duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.FileLogger);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [MSBuildTestMethod]
        [DataRow("distributedfilelogger")]
        [DataRow("DISTRIBUTEDFILELOGGER")]
        [DataRow("DistributedFileLogger")]
        [DataRow("dfl")]
        [DataRow("DFL")]
        public void DistributedFileLoggerSwitchIdentificationTests(string distributedfilelogger)
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            CommandLineSwitches.IsParameterlessSwitch(distributedfilelogger, out parameterlessSwitch, out duplicateSwitchErrorMessage).ShouldBeTrue();
            parameterlessSwitch.ShouldBe(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger);
            duplicateSwitchErrorMessage.ShouldBeNull();
        }

        [MSBuildTestMethod]
        [DataRow("ll")]
        [DataRow("LL")]
        [DataRow("livelogger")]
        [DataRow("LiveLogger")]
        [DataRow("LIVELOGGER")]
        [DataRow("tl")]
        [DataRow("TL")]
        [DataRow("terminallogger")]
        [DataRow("TerminalLogger")]
        [DataRow("TERMINALLOGGER")]
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

        [MSBuildTestMethod]
        [DataRow("flp")]
        [DataRow("FLP")]
        [DataRow("fileLoggerParameters")]
        [DataRow("FILELOGGERPARAMETERS")]
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

        [MSBuildTestMethod]
        [DataRow("tlp")]
        [DataRow("TLP")]
        [DataRow("terminalLoggerParameters")]
        [DataRow("TERMINALLOGGERPARAMETERS")]
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

        [MSBuildTestMethod]
        [DataRow("nr")]
        [DataRow("NR")]
        [DataRow("nodereuse")]
        [DataRow("NodeReuse")]
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

        [MSBuildTestMethod]
        public void ProjectSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch(null, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Project, parameterizedSwitch);
            Assert.IsNotNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            // for the virtual project switch, we match on null, not empty string
            Assert.IsFalse(CommandLineSwitches.IsParameterizedSwitch(String.Empty, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Invalid, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);
        }

        [MSBuildTestMethod]
        [DataRow("ignoreprojectextensions")]
        [DataRow("IgnoreProjectExtensions")]
        [DataRow("IGNOREPROJECTEXTENSIONS")]
        [DataRow("ignore")]
        [DataRow("IGNORE")]
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

        [MSBuildTestMethod]
        [DataRow("target")]
        [DataRow("TARGET")]
        [DataRow("Target")]
        [DataRow("t")]
        [DataRow("T")]
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

        [MSBuildTestMethod]
        [DataRow("property")]
        [DataRow("PROPERTY")]
        [DataRow("Property")]
        [DataRow("p")]
        [DataRow("P")]
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

        [MSBuildTestMethod]
        [DataRow("restoreproperty")]
        [DataRow("RESTOREPROPERTY")]
        [DataRow("RestoreProperty")]
        [DataRow("rp")]
        [DataRow("RP")]
        public void RestorePropertySwitchIdentificationTests(string property)
        {
            CommandLineSwitches.IsParameterizedSwitch(property, out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch, out string duplicateSwitchErrorMessage, out bool multipleParametersAllowed, out string missingParametersErrorMessage, out bool unquoteParameters, out bool emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.RestoreProperty);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldBe("MissingPropertyError");
            unquoteParameters.ShouldBeTrue();
        }

        [MSBuildTestMethod]
        [DataRow("logger")]
        [DataRow("LOGGER")]
        [DataRow("Logger")]
        [DataRow("l")]
        [DataRow("L")]
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

        [MSBuildTestMethod]
        [DataRow("verbosity")]
        [DataRow("VERBOSITY")]
        [DataRow("Verbosity")]
        [DataRow("v")]
        [DataRow("V")]
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

        [MSBuildTestMethod]
        [DataRow("ds")]
        [DataRow("DS")]
        [DataRow("Ds")]
        [DataRow("detailedsummary")]
        [DataRow("DETAILEDSUMMARY")]
        [DataRow("DetailedSummary")]
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

        [MSBuildTestMethod]
        [DataRow("m")]
        [DataRow("M")]
        [DataRow("maxcpucount")]
        [DataRow("MAXCPUCOUNT")]
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

        [MSBuildTestMethod]
        [DataRow("mt")]
        [DataRow("MT")]
        [DataRow("multithreaded")]
        [DataRow("multiThreaded")]
        public void MultiThreadedeParametersIdentificationTests(string multithreaded)
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;
            bool emptyParametersAllowed;

            CommandLineSwitches.IsParameterizedSwitch(multithreaded, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters, out emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.MultiThreaded);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeFalse();
            missingParametersErrorMessage.ShouldBeNull();
            unquoteParameters.ShouldBeTrue();
        }

#if FEATURE_XML_SCHEMA_VALIDATION
        [MSBuildTestMethod]
        [DataRow("validate")]
        [DataRow("VALIDATE")]
        [DataRow("Validate")]
        [DataRow("val")]
        [DataRow("VAL")]
        [DataRow("Val")]
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

        [MSBuildTestMethod]
        [DataRow("preprocess")]
        [DataRow("pp")]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        [DataRow("targets")]
        [DataRow("tArGeTs")]
        [DataRow("ts")]
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

        [MSBuildTestMethod]
        [DataRow("featureavailability")]
        [DataRow("fa")]
        public void FeatureAvailibilitySwitchIdentificationTest(string switchName)
        {
            CommandLineSwitches.IsParameterizedSwitch(
                switchName,
                out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch,
                out string duplicateSwitchErrorMessage,
                out bool multipleParametersAllowed,
                out string missingParametersErrorMessage,
                out _,
                out _);

            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.FeatureAvailability);
            duplicateSwitchErrorMessage.ShouldBeNull();
            multipleParametersAllowed.ShouldBeTrue();
            missingParametersErrorMessage.ShouldNotBeNullOrEmpty();
        }

        [MSBuildTestMethod]
        public void TargetsSwitchParameter()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/targets:targets.txt"], switches);

            switches.HaveErrors().ShouldBeFalse();
            switches[CommandLineSwitches.ParameterizedSwitch.Targets].ShouldBe(new[] { "targets.txt" });
        }

        [MSBuildTestMethod]
        public void TargetsSwitchDoesNotSupportMultipleOccurrences()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/targets /targets"], switches);

            switches.HaveErrors().ShouldBeTrue();
        }

        [MSBuildTestMethod]
        [DataRow("isolate")]
        [DataRow("ISOLATE")]
        [DataRow("isolateprojects")]
        [DataRow("isolateProjects")]
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

        [MSBuildTestMethod]
        [DataRow("graph")]
        [DataRow("GRAPH")]
        [DataRow("graphbuild")]
        [DataRow("graphBuild")]
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

        [MSBuildTestMethod]
        [DataRow("low")]
        [DataRow("LOW")]
        [DataRow("lowpriority")]
        [DataRow("lowPriority")]
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

        [MSBuildTestMethod]
        public void GraphBuildSwitchCanHaveParameters()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/graph", "/graph:true;  NoBuild  ;;  ;", "/graph:foo"], switches);

            switches[CommandLineSwitches.ParameterizedSwitch.GraphBuild].ShouldBe(new[] { "true", "  NoBuild  ", "  ", "foo" });

            switches.HaveErrors().ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void GraphBuildSwitchCanBeParameterless()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/graph"], switches);

            switches[CommandLineSwitches.ParameterizedSwitch.GraphBuild].ShouldBe(Array.Empty<string>());

            switches.HaveErrors().ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void InputResultsCachesSupportsMultipleOccurrence()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/irc", "/irc:a;b", "/irc:c;d"], switches);

            switches[CommandLineSwitches.ParameterizedSwitch.InputResultsCaches].ShouldBe(new[] { null, "a", "b", "c", "d" });

            switches.HaveErrors().ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void OutputResultsCache()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/orc:a"], switches);

            switches[CommandLineSwitches.ParameterizedSwitch.OutputResultsCache].ShouldBe(new[] { "a" });

            switches.HaveErrors().ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void OutputResultsCachesDoesNotSupportMultipleOccurrences()
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/orc:a", "/orc:b"], switches);

            switches.HaveErrors().ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void SetParameterlessSwitchTests()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.Help, "/help");

            Assert.AreEqual("/help", switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switches[CommandLineSwitches.ParameterlessSwitch.Help]);

            // set it again
            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.Help, "-HELP");

            Assert.AreEqual("-HELP", switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switches[CommandLineSwitches.ParameterlessSwitch.Help]);

            // we didn't set this switch
            Assert.IsNull(switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Version));
            Assert.IsFalse(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Version));
            Assert.IsFalse(switches[CommandLineSwitches.ParameterlessSwitch.Version]);
        }

        [MSBuildTestMethod]
        public void SetParameterizedSwitchTests1()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true, false));

            Assert.AreEqual("/v:q", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Verbosity));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Verbosity));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Verbosity];

            Assert.IsNotNull(parameters);
            Assert.ContainsSingle(parameters);
            Assert.AreEqual("q", parameters[0]);

            // set it again -- this is bogus, because the /verbosity switch doesn't allow multiple parameters, but for the
            // purposes of testing the SetParameterizedSwitch() method, it doesn't matter
            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/verbosity:\"diag\";minimal", "\"diag\";minimal", true, true, false));

            Assert.AreEqual("/v:q /verbosity:\"diag\";minimal", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Verbosity));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Verbosity));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Verbosity];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(3, parameters.Length);
            Assert.AreEqual("q", parameters[0]);
            Assert.AreEqual("diag", parameters[1]);
            Assert.AreEqual("minimal", parameters[2]);
        }

        [MSBuildTestMethod]
        public void SetParameterizedSwitchTests2()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            // we haven't set this switch yet
            Assert.IsNull(switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsFalse(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.IsNotNull(parameters);
            Assert.IsEmpty(parameters);

            // fake/missing parameters -- this is bogus because the /target switch allows multiple parameters but we're turning
            // that off here just for testing purposes
            Assert.IsFalse(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"", "\"", false, true, false));

            // switch has been set
            Assert.AreEqual("/t:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            // but no parameters
            Assert.IsNotNull(parameters);
            Assert.IsEmpty(parameters);

            // more fake/missing parameters
            Assert.IsFalse(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:A,\"\";B", "A,\"\";B", true, true, false));

            Assert.AreEqual("/t:\" /t:A,\"\";B", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            // now we have some parameters
            Assert.IsNotNull(parameters);
            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual("A", parameters[0]);
            Assert.AreEqual("B", parameters[1]);
        }

        [MSBuildTestMethod]
        public void SetParameterizedSwitchTests3()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            // we haven't set this switch yet
            Assert.IsNull(switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.IsFalse(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.IsNotNull(parameters);
            Assert.IsEmpty(parameters);

            // don't unquote fake/missing parameters
            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/l:\"", "\"", false, false, false));

            Assert.AreEqual("/l:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.IsNotNull(parameters);
            Assert.ContainsSingle(parameters);
            Assert.AreEqual("\"", parameters[0]);

            // don't unquote multiple fake/missing parameters -- this is bogus because the /logger switch does not take multiple
            // parameters, but for testing purposes this is fine
            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/LOGGER:\"\",asm;\"p,a;r\"", "\"\",asm;\"p,a;r\"", true, false, false));

            Assert.AreEqual("/l:\" /LOGGER:\"\",asm;\"p,a;r\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(4, parameters.Length);
            Assert.AreEqual("\"", parameters[0]);
            Assert.AreEqual("\"\"", parameters[1]);
            Assert.AreEqual("asm", parameters[2]);
            Assert.AreEqual("\"p,a;r\"", parameters[3]);
        }

        [MSBuildTestMethod]
        public void SetParameterizedSwitchTestsAllowEmpty()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.WarningsAsErrors, "/warnaserror", "", multipleParametersAllowed: true, unquoteParameters: false, emptyParametersAllowed: true));

            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.WarningsAsErrors));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.WarningsAsErrors];

            Assert.IsNotNull(parameters);

            Assert.IsTrue(parameters.Length > 0);

            Assert.IsNull(parameters.Last());
        }

        [MSBuildTestMethod]
        public void AppendErrorTests1()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();
            CommandLineSwitches switchesRight = new CommandLineSwitches();

            Assert.IsFalse(switchesLeft.HaveErrors());
            Assert.IsFalse(switchesRight.HaveErrors());

            switchesLeft.Append(switchesRight);

            Assert.IsFalse(switchesLeft.HaveErrors());
            Assert.IsFalse(switchesRight.HaveErrors());

            switchesLeft.SetUnknownSwitchError("/bogus");

            Assert.IsTrue(switchesLeft.HaveErrors());
            Assert.IsFalse(switchesRight.HaveErrors());

            switchesLeft.Append(switchesRight);

            Assert.IsTrue(switchesLeft.HaveErrors());
            Assert.IsFalse(switchesRight.HaveErrors());

            VerifySwitchError(switchesLeft, "/bogus");

            switchesRight.Append(switchesLeft);

            Assert.IsTrue(switchesLeft.HaveErrors());
            Assert.IsTrue(switchesRight.HaveErrors());

            VerifySwitchError(switchesLeft, "/bogus");
            VerifySwitchError(switchesRight, "/bogus");
        }

        [MSBuildTestMethod]
        public void AppendErrorTests2()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();
            CommandLineSwitches switchesRight = new CommandLineSwitches();

            Assert.IsFalse(switchesLeft.HaveErrors());
            Assert.IsFalse(switchesRight.HaveErrors());

            switchesLeft.SetUnknownSwitchError("/bogus");
            switchesRight.SetUnexpectedParametersError("/nologo:foo");

            Assert.IsTrue(switchesLeft.HaveErrors());
            Assert.IsTrue(switchesRight.HaveErrors());

            VerifySwitchError(switchesLeft, "/bogus");
            VerifySwitchError(switchesRight, "/nologo:foo");

            switchesLeft.Append(switchesRight);

            VerifySwitchError(switchesLeft, "/bogus");
            VerifySwitchError(switchesRight, "/nologo:foo");
        }

        [MSBuildTestMethod]
        public void AppendParameterlessSwitchesTests()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.Help, "/?");

            Assert.IsTrue(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsFalse(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));

            CommandLineSwitches switchesRight1 = new CommandLineSwitches();

            switchesRight1.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, "/noconlog");

            Assert.IsFalse(switchesRight1.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switchesRight1.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));

            switchesLeft.Append(switchesRight1);

            Assert.AreEqual("/noconlog", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.IsTrue(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.IsTrue(switchesLeft[CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger]);

            // this switch is not affected
            Assert.AreEqual("/?", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switchesLeft[CommandLineSwitches.ParameterlessSwitch.Help]);

            CommandLineSwitches switchesRight2 = new CommandLineSwitches();

            switchesRight2.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, "/NOCONSOLELOGGER");

            Assert.IsFalse(switchesRight2.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switchesRight2.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));

            switchesLeft.Append(switchesRight2);

            Assert.AreEqual("/NOCONSOLELOGGER", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.IsTrue(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger));
            Assert.IsTrue(switchesLeft[CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger]);

            Assert.AreEqual("/?", switchesLeft.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switchesLeft.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Help));
            Assert.IsTrue(switchesLeft[CommandLineSwitches.ParameterlessSwitch.Help]);

            Assert.IsFalse(switchesLeft.HaveErrors());
        }

        [MSBuildTestMethod]
        public void AppendParameterizedSwitchesTests1()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true, false);

            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsFalse(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:build", "build", true, true, false);

            Assert.IsFalse(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsTrue(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            switchesLeft.Append(switchesRight);

            Assert.AreEqual("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.IsNotNull(parameters);
            Assert.ContainsSingle(parameters);
            Assert.AreEqual("tempproject.proj", parameters[0]);

            Assert.AreEqual("/t:build", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.IsNotNull(parameters);
            Assert.ContainsSingle(parameters);
            Assert.AreEqual("build", parameters[0]);
        }

        [MSBuildTestMethod]
        public void AppendParameterizedSwitchesTests2()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/target:Clean", "Clean", true, true, false);

            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"RESOURCES\";build", "\"RESOURCES\";build", true, true, false);

            Assert.IsTrue(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            switchesLeft.Append(switchesRight);

            Assert.AreEqual("/t:\"RESOURCES\";build", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(3, parameters.Length);
            Assert.AreEqual("Clean", parameters[0]);
            Assert.AreEqual("RESOURCES", parameters[1]);
            Assert.AreEqual("build", parameters[2]);
        }

        /// <summary>
        /// Verifies that the Target property is unquoted and parsed properly.
        /// This will remove the possibility to have the ';' in the target name.
        /// </summary>
        [MSBuildTestMethod]
        [DataRow("/t:Clean;Build", "\"Clean;Build\"")]
        [DataRow("/t:Clean;Build", "Clean;Build")]
        public void ParameterizedSwitchTargetQuotedTest(string commandLineArg, string switchParameters)
        {
            CommandLineSwitches switches = new CommandLineSwitches();
            switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, commandLineArg, switchParameters, true, true, false);
            switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target).ShouldBeTrue();

            switches[CommandLineSwitches.ParameterizedSwitch.Target].Length.ShouldBe(2);
            switches[CommandLineSwitches.ParameterizedSwitch.Target][0].ShouldBe("Clean");
            switches[CommandLineSwitches.ParameterizedSwitch.Target][1].ShouldBe("Build");
            switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target).ShouldBe(commandLineArg);
        }

        /// <summary>
        /// Verifies that the parsing behavior of quoted target properties is not changed when ChangeWave configured.
        /// </summary>
        [MSBuildTestMethod]
        public void ParameterizedSwitchTargetQuotedChangeWaveTest()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", "17.10");
                ChangeWaves.ResetStateForTests();

                CommandLineSwitches switches = new CommandLineSwitches();
                switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:Clean;Build", "\"Clean;Build\"", true, true, false);
                switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target).ShouldBeTrue();

                switches[CommandLineSwitches.ParameterizedSwitch.Target].Length.ShouldBe(1);
                switches[CommandLineSwitches.ParameterizedSwitch.Target][0].ShouldBe("Clean;Build");
            }
        }

        [MSBuildTestMethod]
        public void AppendParameterizedSwitchesTests3()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true, false);

            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "Rhubarb.proj", "Rhubarb.proj", false, true, false);

            Assert.IsTrue(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            switchesLeft.Append(switchesRight);

            Assert.AreEqual("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.IsNotNull(parameters);
            Assert.ContainsSingle(parameters);
            Assert.AreEqual("tempproject.proj", parameters[0]);

            Assert.IsTrue(switchesLeft.HaveErrors());

            VerifySwitchError(switchesLeft, "Rhubarb.proj");
        }

        [MSBuildTestMethod]
        public void InvalidToolsVersionErrors()
        {
            Assert.ThrowsExactly<InitializationException>(() =>
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
                                        false,
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
                                        isTaskAndTargetItemLoggingRequired: false,
                                        isBuildCheckEnabled: false,
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
        [MSBuildTestMethod]
        public void TestHaveAnySwitchesBeenSet()
        {
            // Check if method works with parameterized switch
            CommandLineSwitches switches = new CommandLineSwitches();
            Assert.IsFalse(switches.HaveAnySwitchesBeenSet());
            switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true, false);
            Assert.IsTrue(switches.HaveAnySwitchesBeenSet());

            // Check if method works with parameterless switches
            switches = new CommandLineSwitches();
            Assert.IsFalse(switches.HaveAnySwitchesBeenSet());
            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.Help, "/?");
            Assert.IsTrue(switches.HaveAnySwitchesBeenSet());
        }

        /// <summary>
        /// /nodereuse:false /nodereuse:true should result in "true"
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessNodeReuseSwitchTrueLast()
        {
            bool nodeReuse = MSBuildApp.ProcessNodeReuseSwitch(new string[] { "false", "true" });

            Assert.IsTrue(nodeReuse);
        }

        /// <summary>
        /// /nodereuse:true /nodereuse:false should result in "false"
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessNodeReuseSwitchFalseLast()
        {
            bool nodeReuse = MSBuildApp.ProcessNodeReuseSwitch(new string[] { "true", "false" });

            Assert.IsFalse(nodeReuse);
        }

        /// <summary>
        /// Regress DDB #143341:
        ///     msbuild /clp:v=quiet /clp:v=diag /m:2
        /// gave console logger in quiet verbosity; expected diagnostic
        /// </summary>
        [MSBuildTestMethod]
        public void ExtractAnyLoggerParameterPickLast()
        {
            string result = MSBuildApp.ExtractAnyLoggerParameter("v=diag;v=q", new string[] { "v", "verbosity" });

            Assert.AreEqual("v=q", result);
        }

        /// <summary>
        /// Verifies that when the /warnaserror switch is not specified, the set of warnings is null.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessWarnAsErrorSwitchNotSpecified()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches([""], commandLineSwitches);

            Assert.IsNull(MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches));
        }

        /// <summary>
        /// Verifies that the /warnaserror switch is parsed properly when codes are specified.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessWarnAsErrorSwitchWithCodes()
        {
            ISet<string> expectedWarningsAsErrors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "B", "c", "D", "e" };

            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(
            [
                "\"/warnaserror: a,B ; c \"", // Leading, trailing, leading and trailing whitespace
                "/warnaserror:A,b,C",         // Repeats of different case
                "\"/warnaserror:,    ,,\"",   // Empty items
                "/err:D,d;E,e",               // A different source with new items and uses the short form
                "/warnaserror:a",             // A different source with a single duplicate
                "/warnaserror:a,b",           // A different source with  multiple duplicates
            ], commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.IsNotNull(actualWarningsAsErrors);

            actualWarningsAsErrors.SetEquals(expectedWarningsAsErrors).ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that an empty /warnaserror switch clears the list of codes.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessWarnAsErrorSwitchEmptySwitchClearsSet()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(
            [
                "/warnaserror:a;b;c",
                "/warnaserror",
            ], commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.IsNotNull(actualWarningsAsErrors);

            Assert.IsEmpty(actualWarningsAsErrors);
        }

        /// <summary>
        /// Verifies that when values are specified after an empty /warnaserror switch that they are added to the cleared list.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessWarnAsErrorSwitchValuesAfterEmptyAddOn()
        {
            ISet<string> expectedWarningsAsErors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "e", "f", "g" };

            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(
            [
                "/warnaserror:a;b;c",
                "/warnaserror",
                "/warnaserror:e;f;g",
            ], commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.IsNotNull(actualWarningsAsErrors);

            actualWarningsAsErrors.SetEquals(expectedWarningsAsErors).ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that the /warnaserror switch is parsed properly when no codes are specified.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessWarnAsErrorSwitchEmpty()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/warnaserror"], commandLineSwitches);

            ISet<string> actualWarningsAsErrors = MSBuildApp.ProcessWarnAsErrorSwitch(commandLineSwitches);

            Assert.IsNotNull(actualWarningsAsErrors);

            Assert.IsEmpty(actualWarningsAsErrors);
        }

        /// <summary>
        /// Verifies that when the /warnasmessage switch is used with no values that an error is shown.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessWarnAsMessageSwitchEmpty()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            // Set "expanded" content to match the placeholder so the verify can use the exact resource string as "expected."
            string command = "{0}";
            parser.GatherCommandLineSwitches(["/warnasmessage"], commandLineSwitches, command);

            VerifySwitchError(commandLineSwitches, "/warnasmessage", AssemblyResources.GetString("MissingWarnAsMessageParameterError"));
        }

        /// <summary>
        /// Verify that environment variables cannot be passed in as command line switches.
        /// Also verifies that the full command line is properly passed when a switch error occurs.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessEnvironmentVariableSwitch()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable("ENVIRONMENTVARIABLE", string.Empty);

                CommandLineSwitches commandLineSwitches = new();
                CommandLineParser parser = new CommandLineParser();

                string fullCommandLine = "msbuild validProject.csproj %ENVIRONMENTVARIABLE%";
                parser.GatherCommandLineSwitches(["validProject.csproj", "%ENVIRONMENTVARIABLE%"], commandLineSwitches, fullCommandLine);
                VerifySwitchError(commandLineSwitches, "%ENVIRONMENTVARIABLE%", String.Format(AssemblyResources.GetString("EnvironmentVariableAsSwitch"), fullCommandLine));

                commandLineSwitches = new();
                fullCommandLine = "msbuild %ENVIRONMENTVARIABLE% validProject.csproj";
                parser.GatherCommandLineSwitches(["%ENVIRONMENTVARIABLE%", "validProject.csproj"], commandLineSwitches, fullCommandLine);
                VerifySwitchError(commandLineSwitches, "%ENVIRONMENTVARIABLE%", String.Format(AssemblyResources.GetString("EnvironmentVariableAsSwitch"), fullCommandLine));
            }
        }

        /// <summary>
        /// Verifies that the /warnasmessage switch is parsed properly when codes are specified.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessWarnAsMessageSwitchWithCodes()
        {
            ISet<string> expectedWarningsAsMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "B", "c", "D", "e" };

            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(
            [
                "\"/warnasmessage: a,B ; c \"", // Leading, trailing, leading and trailing whitespace
                "/warnasmessage:A,b,C",         // Repeats of different case
                "\"/warnasmessage:,    ,,\"",   // Empty items
                "/nowarn:D,d;E,e",              // A different source with new items and uses the short form
                "/warnasmessage:a",             // A different source with a single duplicate
                "/warnasmessage:a,b",           // A different source with  multiple duplicates
            ], commandLineSwitches);

            ISet<string> actualWarningsAsMessages = MSBuildApp.ProcessWarnAsMessageSwitch(commandLineSwitches);

            Assert.IsNotNull(actualWarningsAsMessages);

            actualWarningsAsMessages.SetEquals(expectedWarningsAsMessages).ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that when the /profileevaluation switch is used with no values "no-file" is specified.
        /// </summary>
        [MSBuildTestMethod]
        public void ProcessProfileEvaluationEmpty()
        {
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            CommandLineParser parser = new CommandLineParser();

            parser.GatherCommandLineSwitches(["/profileevaluation"], commandLineSwitches);
            commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.ProfileEvaluation][0].ShouldBe("no-file");
        }

        [MSBuildTestMethod]
        public void ProcessBooleanSwitchTest()
        {
            MSBuildApp.ProcessBooleanSwitch(Array.Empty<string>(), defaultValue: true, resourceName: null).ShouldBeTrue();

            MSBuildApp.ProcessBooleanSwitch(Array.Empty<string>(), defaultValue: false, resourceName: null).ShouldBeFalse();

            MSBuildApp.ProcessBooleanSwitch(new[] { "true" }, defaultValue: false, resourceName: null).ShouldBeTrue();

            MSBuildApp.ProcessBooleanSwitch(new[] { "false" }, defaultValue: true, resourceName: null).ShouldBeFalse();

            Should.Throw<CommandLineSwitchException>(() => MSBuildApp.ProcessBooleanSwitch(new[] { "invalid" }, defaultValue: true, resourceName: "InvalidRestoreValue"));
        }

        [MSBuildTestMethod]
        public void NoLogoParameterizedSwitchTest()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            // Test that nologo is now identified as a parameterized switch
            CommandLineSwitches.IsParameterizedSwitch("nologo", out CommandLineSwitches.ParameterizedSwitch parameterizedSwitch, out string duplicateSwitchErrorMessage, out bool multipleParametersAllowed, out string missingParametersErrorMessage, out bool unquoteParameters, out bool emptyParametersAllowed).ShouldBeTrue();
            parameterizedSwitch.ShouldBe(CommandLineSwitches.ParameterizedSwitch.NoLogo);

            // Test setting parameterized nologo switch with explicit true
            switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.NoLogo, "/nologo:true", "true", false, true, false);
            switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.NoLogo).ShouldBeTrue();
            switches[CommandLineSwitches.ParameterizedSwitch.NoLogo][0].ShouldBe("true");

            // Test setting parameterized nologo switch with explicit false
            switches = new CommandLineSwitches();
            switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.NoLogo, "/nologo:false", "false", false, true, false);
            switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.NoLogo).ShouldBeTrue();
            switches[CommandLineSwitches.ParameterizedSwitch.NoLogo][0].ShouldBe("false");
        }

        [MSBuildTestMethod]
        public void NoLogoBooleanProcessingTest()
        {
            // Test ProcessBooleanSwitch behavior for nologo
            // Default value should be true when no parameters are provided
            MSBuildApp.ProcessBooleanSwitch(Array.Empty<string>(), defaultValue: true, resourceName: "InvalidNoLogoValue").ShouldBeTrue();
            MSBuildApp.ProcessBooleanSwitch(Array.Empty<string>(), defaultValue: false, resourceName: "InvalidNoLogoValue").ShouldBeFalse();

            // Test with explicit true/false values
            MSBuildApp.ProcessBooleanSwitch(new[] { "true" }, defaultValue: false, resourceName: "InvalidNoLogoValue").ShouldBeTrue();
            MSBuildApp.ProcessBooleanSwitch(new[] { "false" }, defaultValue: true, resourceName: "InvalidNoLogoValue").ShouldBeFalse();

            // Test invalid value throws exception
            Should.Throw<CommandLineSwitchException>(() => MSBuildApp.ProcessBooleanSwitch(new[] { "invalid" }, defaultValue: true, resourceName: "InvalidNoLogoValue"));
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

        [MSBuildTestMethod]
        [DynamicData(nameof(ProcessGraphBuildSwitchData))]
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
        [MSBuildTestMethod]
        public void ProcessInvalidTargetSwitch()
        {
            string projectContent = """
                <Project>
                </Project>
                """;
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            string project = testEnvironment.CreateTestProjectWithFiles("project.proj", projectContent).ProjectFile;

            MSBuildApp.Execute([@"msbuild.exe", project, "/t:foo.bar"]).ShouldBe(MSBuildApp.ExitType.SwitchError);
        }

        /// <summary>
        /// Verifies that help messages are correctly formed with the right width and leading spaces.
        /// </summary>
        [MSBuildTestMethod]
        public void HelpMessagesAreValid()
        {
            ResourceManager resourceManager = new ResourceManager("MSBuild.Strings", typeof(AssemblyResources).Assembly);

            const string switchLeadingSpaces = "  ";
            const string otherLineLeadingSpaces = "                     ";
            const string examplesLeadingSpaces = "        ";

            foreach (KeyValuePair<string, string> item in resourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)
                .Cast<DictionaryEntry>().Where(i => i.Key is string && ((string)i.Key).StartsWith("HelpMessage_", StringComparison.Ordinal))
                .Select(i => new KeyValuePair<string, string>((string)i.Key, (string)i.Value)))
            {
                string[] helpMessageLines = item.Value.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < helpMessageLines.Length; i++)
                {
                    // All lines should be 80 characters or less
                    Assert.IsTrue(helpMessageLines[i].Length <= 80, $"Line {i + 1} of '{item.Key}' should be no longer than 80 characters.");

                    string trimmedLine = helpMessageLines[i].Trim();

                    if (i == 0)
                    {
                        if (trimmedLine.StartsWith("-", StringComparison.Ordinal) || trimmedLine.StartsWith("@", StringComparison.Ordinal))
                        {
                            // If the first line in a switch it needs a certain amount of leading spaces
                            Assert.StartsWith(switchLeadingSpaces, helpMessageLines[i]);
                        }
                        else
                        {
                            // Otherwise it should have no leading spaces because it's a section
                            Assert.IsFalse(helpMessageLines[i].StartsWith(" ", StringComparison.Ordinal));
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
                            else if (trimmedLine.StartsWith("-", StringComparison.Ordinal) || trimmedLine.StartsWith("@", StringComparison.Ordinal))
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
                Assert.AreEqual(badCommandLineArg, e.CommandLineArg);

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
                Assert.IsTrue(caughtError);
            }
        }
    }
}
