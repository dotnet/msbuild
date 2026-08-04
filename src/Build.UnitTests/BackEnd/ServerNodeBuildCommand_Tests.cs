// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for the <see cref="ServerNodeBuildCommand"/> packet.
    /// </summary>
    public class ServerNodeBuildCommand_Tests
    {
        /// <summary>
        /// Round-trips a <see cref="ServerNodeBuildCommand"/> through the binary translator and verifies that all
        /// fields - including the <see cref="ServerNodeBuildCommand.ShutdownAfterBuild"/> flag that drives the
        /// server's self-teardown - survive serialization for both values of the flag.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RoundTripSerializationPreservesFields(bool shutdownAfterBuild)
        {
            string[] commandLine = ["msbuild.exe", "project.proj", "-mt", "-nr:false"];
            string startupDirectory = "C:\\some\\startup\\dir";
            Dictionary<string, string> buildProcessEnvironment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["VAR1"] = "value1",
                ["VAR2"] = "value2",
            };
            CultureInfo culture = new("en-US");
            CultureInfo uiCulture = new("en-GB");
            TargetConsoleConfiguration consoleConfiguration = new(bufferWidth: 80, acceptAnsiColorCodes: true, outputIsScreen: false, backgroundColor: ConsoleColor.Black);

            ServerNodeBuildCommand command = new(
                commandLine,
                startupDirectory,
                buildProcessEnvironment,
                culture,
                uiCulture,
                consoleConfiguration,
                partialBuildTelemetry: null,
                shutdownAfterBuild);

            ((INodePacket)command).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = ServerNodeBuildCommand.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            ServerNodeBuildCommand deserialized = packet.ShouldBeOfType<ServerNodeBuildCommand>();

            deserialized.ShutdownAfterBuild.ShouldBe(shutdownAfterBuild);
            deserialized.CommandLine.ShouldBe(commandLine);
            deserialized.StartupDirectory.ShouldBe(startupDirectory);
            deserialized.BuildProcessEnvironment.ShouldBe(buildProcessEnvironment);
            deserialized.Culture.ShouldBe(culture);
            deserialized.UICulture.ShouldBe(uiCulture);
        }
    }
}
