// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains all of the information necessary for a entry node to run a command line.
    /// </summary>
    internal sealed class ServerNodeBuildCommand : INodePacket
    {
        private string _commandLine = default!;
        private string _startupDirectory = default!;
        private Dictionary<string, string> _buildProcessEnvironment = default!;
        private CultureInfo _culture = default!;
        private CultureInfo _uiCulture = default!;
        private TargetConsoleConfiguration _consoleConfiguration = default!;
        private PartialBuildTelemetry? _partialBuildTelemetry = default;

        /// <summary>
        /// Retrieves the packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeBuildCommand;

        /// <summary>
        /// Command line including arguments
        /// </summary>
        public string CommandLine => _commandLine;

        /// <summary>
        /// The startup directory
        /// </summary>
        public string StartupDirectory => _startupDirectory;

        /// <summary>
        /// The process environment.
        /// </summary>
        public Dictionary<string, string> BuildProcessEnvironment => _buildProcessEnvironment;

        /// <summary>
        /// The culture
        /// </summary>
        public CultureInfo Culture => _culture;

        /// <summary>
        /// The UI culture.
        /// </summary>
        public CultureInfo UICulture => _uiCulture;

        /// <summary>
        /// Console configuration of Client.
        /// </summary>
        public TargetConsoleConfiguration ConsoleConfiguration => _consoleConfiguration;

        /// <summary>
        /// Part of BuildTelemetry which is collected on client and needs to be sent to server,
        /// so server can log BuildTelemetry once it is finished.
        /// </summary>
        public PartialBuildTelemetry? PartialBuildTelemetry => _partialBuildTelemetry;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeBuildCommand()
        {
        }

        public ServerNodeBuildCommand(
            string commandLine,
            string startupDirectory,
            Dictionary<string, string> buildProcessEnvironment,
            CultureInfo culture, CultureInfo uiCulture,
            TargetConsoleConfiguration consoleConfiguration,
            PartialBuildTelemetry? partialBuildTelemetry)
        {
            ErrorUtilities.VerifyThrowInternalNull(consoleConfiguration);

            _commandLine = commandLine;
            _startupDirectory = startupDirectory;
            _buildProcessEnvironment = buildProcessEnvironment;
            _culture = culture;
            _uiCulture = uiCulture;
            _consoleConfiguration = consoleConfiguration;
            _partialBuildTelemetry = partialBuildTelemetry;
        }

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        /// <param name="translator">The translator to use.</param>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _commandLine);
            translator.Translate(ref _startupDirectory);
            translator.TranslateDictionary(ref _buildProcessEnvironment, StringComparer.OrdinalIgnoreCase);
            translator.TranslateCulture(ref _culture);
            translator.TranslateCulture(ref _uiCulture);
            translator.Translate(ref _consoleConfiguration, TargetConsoleConfiguration.FactoryForDeserialization);
            translator.Translate(ref _partialBuildTelemetry, PartialBuildTelemetry.FactoryForDeserialization);
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            ServerNodeBuildCommand command = new();
            command.Translate(translator);

            return command;
        }
    }
}
