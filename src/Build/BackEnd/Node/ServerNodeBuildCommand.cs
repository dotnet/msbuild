// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains all of the information necessary for a entry node to run a command line.
    /// </summary>
    internal sealed class ServerNodeBuildCommand : INodePacket
    {
#if FEATURE_GET_COMMANDLINE
        private string _commandLine = default!;
#else
        private string[] _commandLine = default!;
#endif
        private string _startupDirectory = default!;
        private Dictionary<string, string> _buildProcessEnvironment = default!;
        private CultureInfo _culture = default!;
        private CultureInfo _uiCulture = default!;

        /// <summary>
        /// Retrieves the packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeBuildCommand;

        /// <summary>
        /// Command line including arguments
        /// </summary>
#if FEATURE_GET_COMMANDLINE
        public string CommandLine => _commandLine;
#else
        public string[] CommandLine => _commandLine;
#endif

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
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeBuildCommand()
        {
        }

        public ServerNodeBuildCommand(
#if FEATURE_GET_COMMANDLINE
            string commandLine,
#else
            string[] commandLine,
#endif
            string startupDirectory,
            Dictionary<string, string> buildProcessEnvironment,
            CultureInfo culture, CultureInfo uiCulture)
        {
            _commandLine = commandLine;
            _startupDirectory = startupDirectory;
            _buildProcessEnvironment = buildProcessEnvironment;
            _culture = culture;
            _uiCulture = uiCulture;
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
