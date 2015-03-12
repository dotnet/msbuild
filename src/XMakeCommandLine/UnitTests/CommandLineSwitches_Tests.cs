// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.CommandLine;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class CommandLineSwitchesTests
    {
        [ClassInitialize]
        public static void Setup(TestContext testContext)
        {
            // Make sure resources are initialized
            MSBuildApp.Initialize();
        }

        [TestMethod]
        public void BogusSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsFalse(CommandLineSwitches.IsParameterlessSwitch("bogus", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Invalid, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsFalse(CommandLineSwitches.IsParameterizedSwitch("bogus", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Invalid, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);
        }

        [TestMethod]
        public void HelpSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("help", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("HELP", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("Help", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("h", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("H", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("?", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void VersionSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("version", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("Version", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("VERSION", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("ver", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("VER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("Ver", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void NoLogoSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("nologo", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoLogo, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NOLOGO", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoLogo, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NoLogo", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoLogo, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void NoAutoResponseSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("noautoresponse", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NOAUTORESPONSE", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NoAutoResponse", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("noautorsp", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NOAUTORSP", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NoAutoRsp", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void NoConsoleLoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("noconsolelogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NOCONSOLELOGGER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NoConsoleLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("noconlog", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NOCONLOG", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("NoConLog", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void FileLoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("fileLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("FILELOGGER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("FileLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("fl", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("FL", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void DistributedFileLoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("distributedfilelogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("DISTRIBUTEDFILELOGGER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("DistributedFileLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("dfl", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("DFL", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void FileLoggerParametersIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("flp", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("FLP", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("fileLoggerParameters", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("FILELOGGERPARAMETERS", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }


        [TestMethod]
        public void NodeReuseParametersIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("nr", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("NR", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("nodereuse", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("NodeReuse", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void ProjectSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch(null, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Project, parameterizedSwitch);
            Assert.IsNotNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            // for the virtual project switch, we match on null, not empty string
            Assert.IsFalse(CommandLineSwitches.IsParameterizedSwitch(String.Empty, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Invalid, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);
        }

        [TestMethod]
        public void IgnoreProjectExtensionsSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("ignoreprojectextensions", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("IgnoreProjectExtensions", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("IGNOREPROJECTEXTENSIONS", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("ignore", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("IGNORE", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void TargetSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("target", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("TARGET", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("Target", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("t", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("T", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void PropertySwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("property", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("PROPERTY", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("Property", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("p", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("P", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsTrue(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void LoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("logger", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("LOGGER", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("Logger", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("l", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("L", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsFalse(unquoteParameters);
        }

        [TestMethod]
        public void VerbositySwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("verbosity", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("VERBOSITY", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("Verbosity", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("v", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("V", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void DetailedSummarySwitchIndentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;
            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("ds", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("DS", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("Ds", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("detailedsummary", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("DETAILEDSUMMARY", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);

            Assert.IsTrue(CommandLineSwitches.IsParameterlessSwitch("DetailedSummary", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.AreEqual(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
        }

        [TestMethod]
        public void MaxCPUCountSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("m", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("M", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("maxcpucount", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("MAXCPUCOUNT", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNotNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void ValidateSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("validate", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("VALIDATE", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("Validate", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("val", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("VAL", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("Val", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void PreprocessSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("preprocess", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Preprocess, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);

            Assert.IsTrue(CommandLineSwitches.IsParameterizedSwitch("pp", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.AreEqual(CommandLineSwitches.ParameterizedSwitch.Preprocess, parameterizedSwitch);
            Assert.IsNull(duplicateSwitchErrorMessage);
            Assert.IsFalse(multipleParametersAllowed);
            Assert.IsNull(missingParametersErrorMessage);
            Assert.IsTrue(unquoteParameters);
        }

        [TestMethod]
        public void SetParameterlessSwitchTests()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoLogo, "/nologo");

            Assert.AreEqual("/nologo", switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.IsTrue(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.IsTrue(switches[CommandLineSwitches.ParameterlessSwitch.NoLogo]);

            // set it again
            switches.SetParameterlessSwitch(CommandLineSwitches.ParameterlessSwitch.NoLogo, "-NOLOGO");

            Assert.AreEqual("-NOLOGO", switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.IsTrue(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.NoLogo));
            Assert.IsTrue(switches[CommandLineSwitches.ParameterlessSwitch.NoLogo]);

            // we didn't set this switch
            Assert.IsNull(switches.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.Version));
            Assert.IsFalse(switches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Version));
            Assert.IsFalse(switches[CommandLineSwitches.ParameterlessSwitch.Version]);
        }

        [TestMethod]
        public void SetParameterizedSwitchTests1()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true));

            Assert.AreEqual("/v:q", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Verbosity));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Verbosity));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Verbosity];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual("q", parameters[0]);

            // set it again -- this is bogus, because the /verbosity switch doesn't allow multiple parameters, but for the
            // purposes of testing the SetParameterizedSwitch() method, it doesn't matter
            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/verbosity:\"diag\";minimal", "\"diag\";minimal", true, true));

            Assert.AreEqual("/verbosity:\"diag\";minimal", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Verbosity));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Verbosity));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Verbosity];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(3, parameters.Length);
            Assert.AreEqual("q", parameters[0]);
            Assert.AreEqual("diag", parameters[1]);
            Assert.AreEqual("minimal", parameters[2]);
        }

        [TestMethod]
        public void SetParameterizedSwitchTests2()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            // we haven't set this switch yet
            Assert.IsNull(switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsFalse(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(0, parameters.Length);

            // fake/missing parameters -- this is bogus because the /target switch allows multiple parameters but we're turning
            // that off here just for testing purposes
            Assert.IsFalse(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"", "\"", false, true));

            // switch has been set
            Assert.AreEqual("/t:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            // but no parameters
            Assert.IsNotNull(parameters);
            Assert.AreEqual(0, parameters.Length);

            // more fake/missing parameters
            Assert.IsFalse(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:A,\"\";B", "A,\"\";B", true, true));

            Assert.AreEqual("/t:A,\"\";B", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            // now we have some parameters
            Assert.IsNotNull(parameters);
            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual("A", parameters[0]);
            Assert.AreEqual("B", parameters[1]);
        }

        [TestMethod]
        public void SetParameterizedSwitchTests3()
        {
            CommandLineSwitches switches = new CommandLineSwitches();

            // we haven't set this switch yet
            Assert.IsNull(switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.IsFalse(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(0, parameters.Length);

            // don't unquote fake/missing parameters
            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/l:\"", "\"", false, false));

            Assert.AreEqual("/l:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual("\"", parameters[0]);

            // don't unquote multiple fake/missing parameters -- this is bogus because the /logger switch does not take multiple
            // parameters, but for testing purposes this is fine
            Assert.IsTrue(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/LOGGER:\"\",asm;\"p,a;r\"", "\"\",asm;\"p,a;r\"", true, false));

            Assert.AreEqual("/LOGGER:\"\",asm;\"p,a;r\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.IsTrue(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(4, parameters.Length);
            Assert.AreEqual("\"", parameters[0]);
            Assert.AreEqual("\"\"", parameters[1]);
            Assert.AreEqual("asm", parameters[2]);
            Assert.AreEqual("\"p,a;r\"", parameters[3]);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void AppendParameterizedSwitchesTests1()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true);

            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsFalse(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:build", "build", true, true);

            Assert.IsFalse(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsTrue(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            switchesLeft.Append(switchesRight);

            Assert.AreEqual("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual("tempproject.proj", parameters[0]);

            Assert.AreEqual("/t:build", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual("build", parameters[0]);
        }

        [TestMethod]
        public void AppendParameterizedSwitchesTests2()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/target:Clean", "Clean", true, true);

            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"RESOURCES\";build", "\"RESOURCES\";build", true, true);

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

        [TestMethod]
        public void AppendParameterizedSwitchesTests3()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true);

            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "Rhubarb.proj", "Rhubarb.proj", false, true);

            Assert.IsTrue(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            switchesLeft.Append(switchesRight);

            Assert.AreEqual("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.IsTrue(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.IsNotNull(parameters);
            Assert.AreEqual(1, parameters.Length);
            Assert.AreEqual("tempproject.proj", parameters[0]);

            Assert.IsTrue(switchesLeft.HaveErrors());

            VerifySwitchError(switchesLeft, "Rhubarb.proj");
        }

        [TestMethod]
        [ExpectedException(typeof(InitializationException))]
        public void InvalidToolsVersionErrors()
        {
            string filename = null;
            try
            {
                filename = FileUtilities.GetTemporaryFile();
                ProjectRootElement project = ProjectRootElement.Create();
                project.Save(filename);
                MSBuildApp.BuildProject(filename, null, "ScoobyDoo", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new ILogger[] { }, LoggerVerbosity.Normal, new DistributedLoggerRecord[] { }, false, null, 1, true, new StringWriter(), false, false);
            }
            finally
            {
                if (File.Exists(filename)) File.Delete(filename);
            }
        }

        [TestMethod]
        public void TestHaveAnySwitchesBeenSet()
        {
            // Check if method works with parameterized switch
            CommandLineSwitches switches = new CommandLineSwitches();
            Assert.IsFalse(switches.HaveAnySwitchesBeenSet());
            switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true);
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
        [TestMethod]
        public void ProcessNodeReuseSwitchTrueLast()
        {
            bool nodeReuse = MSBuildApp.ProcessNodeReuseSwitch(new string[] { "false", "true" });

            Assert.IsTrue(nodeReuse);
        }

        /// <summary>
        /// /nodereuse:true /nodereuse:false should result in "false"
        /// </summary>
        [TestMethod]
        public void ProcessNodeReuseSwitchFalseLast()
        {
            bool nodeReuse = MSBuildApp.ProcessNodeReuseSwitch(new string[] { "true", "false" });

            Assert.AreEqual(false, nodeReuse);
        }

        /// <summary>
        /// Regress DDB #143341: 
        ///     msbuild /clp:v=quiet /clp:v=diag /m:2
        /// gave console logger in quiet verbosity; expected diagnostic
        /// </summary>
        [TestMethod]
        public void ExtractAnyLoggerParameterPickLast()
        {
            string result = MSBuildApp.ExtractAnyLoggerParameter("v=diag;v=q", new string[] { "v", "verbosity" });

            Assert.AreEqual("v=q", result);
        }

        /// <summary>
        /// Verifies that a switch collection has an error registered for the given command line arg.
        /// </summary>
        /// <param name="switches"></param>
        /// <param name="badCommandLineArg"></param>
        private void VerifySwitchError(CommandLineSwitches switches, string badCommandLineArg)
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
