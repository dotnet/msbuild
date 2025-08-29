// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class LoggerDescription_Tests
    {
        [Fact]
        public void LoggerDescriptionCustomSerialization()
        {
            const string className = "Class";
            const string loggerAssemblyName = "Class";
            const string loggerFileAssembly = null;
            const string loggerSwitchParameters = "Class";
            const LoggerVerbosity verbosity = LoggerVerbosity.Detailed;

            LoggerDescription description = new LoggerDescription(className, loggerAssemblyName, loggerFileAssembly, loggerSwitchParameters, verbosity);
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            using BinaryReader reader = new BinaryReader(stream);

            stream.Position = 0;
            description.WriteToStream(writer);
            long streamWriteEndPosition = stream.Position;
            stream.Position = 0;
            LoggerDescription description2 = new LoggerDescription();
            description2.CreateFromStream(reader);
            long streamReadEndPosition = stream.Position;
            Assert.Equal(streamWriteEndPosition, streamReadEndPosition); // "Stream end positions should be equal"

            Assert.Equal(description.Verbosity, description2.Verbosity); // "Expected Verbosity to Match"
            Assert.Equal(description.LoggerId, description2.LoggerId); // "Expected Verbosity to Match"
            Assert.Equal(0, string.Compare(description.LoggerSwitchParameters, description2.LoggerSwitchParameters, StringComparison.OrdinalIgnoreCase)); // "Expected LoggerSwitchParameters to Match"
            Assert.Equal(0, string.Compare(description.Name, description2.Name, StringComparison.OrdinalIgnoreCase)); // "Expected Name to Match"
        }
    }
}
