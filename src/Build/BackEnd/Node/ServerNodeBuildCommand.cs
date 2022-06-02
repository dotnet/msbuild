// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        private string _commandLine = default!;
        private string _startupDirectory = default!;
        private Dictionary<string, string> _buildProcessEnvironment = default!;
        private CultureInfo _culture = default!;
        private CultureInfo _uiCulture = default!;
        private int _consoleBufferWidth = default;
        private bool _acceptAnsiColorCodes = default;
        private ConsoleColor _consoleBackgroundColor = default;
        private bool _consoleIsScreen = default;

        /// <summary>
        /// Retrieves the packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeBuildCommand;

        /// <summary>
        /// The startup directory
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
        /// Buffer width of destination Console.
        /// Console loggers are supposed, on Windows OS, to be wrapping to avoid output trimming.
        /// -1 console buffer width can't be obtained.
        /// </summary>
        public int ConsoleBufferWidth => _consoleBufferWidth;

        /// <summary>
        /// True if console output accept ANSI colors codes.
        /// False if output is redirected to non screen type such as file or nul.
        /// </summary>
        public bool AcceptAnsiColorCodes => _acceptAnsiColorCodes;

        /// <summary>
        /// True if console output is screen. It is expected that non screen output is post-processed and often does not need wrapping and coloring.
        /// False if output is redirected to non screen type such as file or nul.
        /// </summary>
        public bool ConsoleIsScreen => _consoleIsScreen;

        /// <summary>
        /// Background color of client console, -1 if not detectable
        /// </summary>
        public ConsoleColor ConsoleBackgroundColor => _consoleBackgroundColor;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeBuildCommand()
        {
        }

        public ServerNodeBuildCommand(string commandLine, string startupDirectory, Dictionary<string, string> buildProcessEnvironment, CultureInfo culture, CultureInfo uiCulture,
            int consoleBufferWidth, bool acceptAnsiColorCodes, bool consoleIsScreen, ConsoleColor consoleBackgroundColor)
        {
            _commandLine = commandLine;
            _startupDirectory = startupDirectory;
            _buildProcessEnvironment = buildProcessEnvironment;
            _culture = culture;
            _uiCulture = uiCulture;

            _consoleBufferWidth = consoleBufferWidth;
            _acceptAnsiColorCodes = acceptAnsiColorCodes;
            _consoleIsScreen = consoleIsScreen;
            _consoleBackgroundColor = consoleBackgroundColor;
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
            translator.Translate(ref _consoleBufferWidth);
            translator.Translate(ref _acceptAnsiColorCodes);
            translator.Translate(ref _consoleIsScreen);
            translator.TranslateEnum(ref _consoleBackgroundColor, (int)_consoleBackgroundColor);
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
