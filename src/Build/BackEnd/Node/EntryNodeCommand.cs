// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Build.BackEnd
{
    internal class EntryNodeConsoleWrite : INodePacket
    {
        public string Text { get; }
        public ConsoleColor Foreground { get; }
        public ConsoleColor Background { get; }

        /// <summary>
        /// 1 = stdout, 2 = stderr
        /// </summary>
        public byte OutputType { get; }

        public EntryNodeConsoleWrite(string text, ConsoleColor foreground, ConsoleColor background, byte outputType)
        {
            Text = text;
            Foreground = foreground;
            Background = background;
            OutputType = outputType;
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.EntryNodeInfo
        /// </summary>
        public NodePacketType Type => NodePacketType.EntryNodeConsole;

        #endregion

        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var bw = translator.Writer;

                bw.Write(Text);
                bw.Write((int)Foreground);
                bw.Write((int)Background);
                bw.Write(OutputType);
            }
            else
            {
                throw new InvalidOperationException("Read from stream not supported");
            }
        }
    }

    internal class EntryNodeResponse : INodePacket
    {
        public EntryNodeResponse(int exitCode, string exitType)
        {
            ExitCode = exitCode;
            ExitType = exitType;
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.EntryNodeCommand
        /// </summary>
        public NodePacketType Type => NodePacketType.EntryNodeResponse;

        #endregion

        public int ExitCode { get; }

        public string ExitType { get; }

        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var bw = translator.Writer;

                bw.Write(ExitCode);
                bw.Write(ExitType);
            }
            else
            {
                throw new InvalidOperationException("Read from stream not supported");
            }
        }
    }

    /// <summary>
    /// EntryNodeCommand contains all of the information necessary for a entry node to run a command line.
    /// </summary>
    internal class EntryNodeCommand : INodePacket
    {
        /// <summary>
        /// The startup directory
        /// </summary>
        private string _commandLine;

        /// <summary>
        /// The startup directory
        /// </summary>
        private string _startupDirectory;

        /// <summary>
        /// The process environment.
        /// </summary>
        private Dictionary<string, string> _buildProcessEnvironment;

        /// <summary>
        /// The culture
        /// </summary>
        private CultureInfo _culture;

        /// <summary>
        /// The UI culture.
        /// </summary>
        private CultureInfo _uiCulture;

        public EntryNodeCommand(string commandLine, string startupDirectory, Dictionary<string, string> buildProcessEnvironment, CultureInfo culture, CultureInfo uiCulture)
        {
            _commandLine = commandLine;
            _startupDirectory = startupDirectory;
            _buildProcessEnvironment = buildProcessEnvironment;
            _culture = culture;
            _uiCulture = uiCulture;
        }

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private EntryNodeCommand()
        {
        }

#region INodePacket Members

        /// <summary>
        /// Retrieves the packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.EntryNodeCommand;

#endregion

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

#region INodePacketTranslatable Members

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        /// <param name="translator">The translator to use.</param>
        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                var br = translator.Reader;

                _commandLine = br.ReadString();
                _startupDirectory = br.ReadString();
                int count = br.ReadInt32();
                _buildProcessEnvironment = new Dictionary<string, string>(count, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < count; ++i)
                {
                    var key = br.ReadString();
                    var value = br.ReadString();
                    _buildProcessEnvironment.Add(key, value);
                }
                _culture = new CultureInfo(br.ReadString());
                _uiCulture = new CultureInfo(br.ReadString());
            }
            else
            {
                throw new InvalidOperationException("Writing into stream not supported");
            }
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            EntryNodeCommand command = new ();
            command.Translate(translator);
            return command;
        }
#endregion
    }
}
