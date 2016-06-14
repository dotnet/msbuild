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
using Xunit;

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

            Assert.False(CommandLineSwitches.IsParameterlessSwitch("bogus", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Invalid, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.False(CommandLineSwitches.IsParameterizedSwitch("bogus", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Invalid, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.False(unquoteParameters);
        }

        [Fact]
        public void HelpSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("help", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("HELP", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("Help", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("h", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("H", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("?", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Help, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void VersionSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("version", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("Version", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("VERSION", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("ver", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("VER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("Ver", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.Version, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void NoLogoSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("nologo", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoLogo, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NOLOGO", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoLogo, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NoLogo", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoLogo, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void NoAutoResponseSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("noautoresponse", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NOAUTORESPONSE", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NoAutoResponse", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("noautorsp", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NOAUTORSP", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NoAutoRsp", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void NoConsoleLoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("noconsolelogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NOCONSOLELOGGER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NoConsoleLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("noconlog", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NOCONLOG", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("NoConLog", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void FileLoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("fileLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("FILELOGGER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("FileLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("fl", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("FL", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.FileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void DistributedFileLoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("distributedfilelogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("DISTRIBUTEDFILELOGGER", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("DistributedFileLogger", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("dfl", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("DFL", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void FileLoggerParametersIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("flp", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("FLP", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("fileLoggerParameters", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("FILELOGGERPARAMETERS", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
        }

#if FEATURE_NODE_REUSE
        [Fact]
        public void NodeReuseParametersIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("nr", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("NR", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("nodereuse", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("NodeReuse", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.NodeReuse, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
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

            Assert.True(CommandLineSwitches.IsParameterizedSwitch(null, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Project, parameterizedSwitch);
            Assert.NotNull(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            // for the virtual project switch, we match on null, not empty string
            Assert.False(CommandLineSwitches.IsParameterizedSwitch(String.Empty, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Invalid, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.False(unquoteParameters);
        }

        [Fact]
        public void IgnoreProjectExtensionsSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("ignoreprojectextensions", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("IgnoreProjectExtensions", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("IGNOREPROJECTEXTENSIONS", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("ignore", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("IGNORE", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
        }

        [Fact]
        public void TargetSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("target", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("TARGET", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("Target", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("t", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("T", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Target, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
        }

        [Fact]
        public void PropertySwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("property", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("PROPERTY", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("Property", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("p", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("P", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Property, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.True(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
        }

        [Fact]
        public void LoggerSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("logger", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.False(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("LOGGER", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.False(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("Logger", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.False(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("l", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.False(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("L", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Logger, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.False(unquoteParameters);
        }

        [Fact]
        public void VerbositySwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("verbosity", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("VERBOSITY", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("Verbosity", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("v", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("V", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Verbosity, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
        }

        [Fact]
        public void DetailedSummarySwitchIndentificationTests()
        {
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
            string duplicateSwitchErrorMessage;
            Assert.True(CommandLineSwitches.IsParameterlessSwitch("ds", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("DS", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("Ds", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("detailedsummary", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("DETAILEDSUMMARY", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);

            Assert.True(CommandLineSwitches.IsParameterlessSwitch("DetailedSummary", out parameterlessSwitch, out duplicateSwitchErrorMessage));
            Assert.Equal(CommandLineSwitches.ParameterlessSwitch.DetailedSummary, parameterlessSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
        }

        [Fact]
        public void MaxCPUCountSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("m", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("M", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("maxcpucount", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("MAXCPUCOUNT", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.MaxCPUCount, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.NotNull(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
        }

#if FEATURE_XML_SCHEMA_VALIDATION
        [Fact]
        public void ValidateSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("validate", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("VALIDATE", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("Validate", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("val", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("VAL", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("Val", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Validate, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
        }
#endif

        [Fact]
        public void PreprocessSwitchIdentificationTests()
        {
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
            string duplicateSwitchErrorMessage;
            bool multipleParametersAllowed;
            string missingParametersErrorMessage;
            bool unquoteParameters;

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("preprocess", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Preprocess, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);

            Assert.True(CommandLineSwitches.IsParameterizedSwitch("pp", out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters));
            Assert.Equal(CommandLineSwitches.ParameterizedSwitch.Preprocess, parameterizedSwitch);
            Assert.Null(duplicateSwitchErrorMessage);
            Assert.False(multipleParametersAllowed);
            Assert.Null(missingParametersErrorMessage);
            Assert.True(unquoteParameters);
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

            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true));

            Assert.Equal("/v:q", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Verbosity));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Verbosity));

            string[] parameters = switches[CommandLineSwitches.ParameterizedSwitch.Verbosity];

            Assert.NotNull(parameters);
            Assert.Equal(1, parameters.Length);
            Assert.Equal("q", parameters[0]);

            // set it again -- this is bogus, because the /verbosity switch doesn't allow multiple parameters, but for the
            // purposes of testing the SetParameterizedSwitch() method, it doesn't matter
            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/verbosity:\"diag\";minimal", "\"diag\";minimal", true, true));

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
            Assert.Equal(0, parameters.Length);

            // fake/missing parameters -- this is bogus because the /target switch allows multiple parameters but we're turning
            // that off here just for testing purposes
            Assert.False(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"", "\"", false, true));

            // switch has been set
            Assert.Equal("/t:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Target];

            // but no parameters
            Assert.NotNull(parameters);
            Assert.Equal(0, parameters.Length);

            // more fake/missing parameters
            Assert.False(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:A,\"\";B", "A,\"\";B", true, true));

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
            Assert.Equal(0, parameters.Length);

            // don't unquote fake/missing parameters
            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/l:\"", "\"", false, false));

            Assert.Equal("/l:\"", switches.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Logger));
            Assert.True(switches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Logger));

            parameters = switches[CommandLineSwitches.ParameterizedSwitch.Logger];

            Assert.NotNull(parameters);
            Assert.Equal(1, parameters.Length);
            Assert.Equal("\"", parameters[0]);

            // don't unquote multiple fake/missing parameters -- this is bogus because the /logger switch does not take multiple
            // parameters, but for testing purposes this is fine
            Assert.True(switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Logger, "/LOGGER:\"\",asm;\"p,a;r\"", "\"\",asm;\"p,a;r\"", true, false));

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

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true);

            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.False(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:build", "build", true, true);

            Assert.False(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.True(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            switchesLeft.Append(switchesRight);

            Assert.Equal("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.NotNull(parameters);
            Assert.Equal(1, parameters.Length);
            Assert.Equal("tempproject.proj", parameters[0]);

            Assert.Equal("/t:build", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Target));
            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Target];

            Assert.NotNull(parameters);
            Assert.Equal(1, parameters.Length);
            Assert.Equal("build", parameters[0]);
        }

        [Fact]
        public void AppendParameterizedSwitchesTests2()
        {
            CommandLineSwitches switchesLeft = new CommandLineSwitches();

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/target:Clean", "Clean", true, true);

            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Target));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Target, "/t:\"RESOURCES\";build", "\"RESOURCES\";build", true, true);

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

            switchesLeft.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "tempproject.proj", "tempproject.proj", false, true);

            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            CommandLineSwitches switchesRight = new CommandLineSwitches();

            switchesRight.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Project, "Rhubarb.proj", "Rhubarb.proj", false, true);

            Assert.True(switchesRight.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            switchesLeft.Append(switchesRight);

            Assert.Equal("tempproject.proj", switchesLeft.GetParameterizedSwitchCommandLineArg(CommandLineSwitches.ParameterizedSwitch.Project));
            Assert.True(switchesLeft.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Project));

            string[] parameters = switchesLeft[CommandLineSwitches.ParameterizedSwitch.Project];

            Assert.NotNull(parameters);
            Assert.Equal(1, parameters.Length);
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
                    filename = FileUtilities.GetTemporaryFile();
                    ProjectRootElement project = ProjectRootElement.Create();
                    project.Save(filename);
                    MSBuildApp.BuildProject(
                                        filename,
                                        null,
                                        "ScoobyDoo",
                                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                                        new ILogger[] { },
                                        LoggerVerbosity.Normal,
                                        new DistributedLoggerRecord[] { },
#if FEATURE_XML_SCHEMA_VALIDATION
                                        false,
                                        null,
#endif
                                        1,
                                        true,
                                        new StringWriter(),
                                        false,
                                        false);
                }
                finally
                {
                    if (File.Exists(filename)) File.Delete(filename);
                }
            }
           );
        }
        [Fact]
        public void TestHaveAnySwitchesBeenSet()
        {
            // Check if method works with parameterized switch
            CommandLineSwitches switches = new CommandLineSwitches();
            Assert.False(switches.HaveAnySwitchesBeenSet());
            switches.SetParameterizedSwitch(CommandLineSwitches.ParameterizedSwitch.Verbosity, "/v:q", "q", false, true);
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

            Assert.Equal(false, nodeReuse);
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
                Assert.Equal(badCommandLineArg, e.CommandLineArg);

                caughtError = true;

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
