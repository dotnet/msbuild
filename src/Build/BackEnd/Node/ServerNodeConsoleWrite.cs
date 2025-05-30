// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    internal sealed class ServerNodeConsoleWrite : INodePacket
    {
        private string _text = default!;
        private ConsoleOutput _outputType = default!;

        /// <summary>
        /// Packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeConsoleWrite;

        public string Text => _text;

        /// <summary>
        /// Console output for the message
        /// </summary>
        public ConsoleOutput OutputType => _outputType;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeConsoleWrite() { }

        public ServerNodeConsoleWrite(string text, ConsoleOutput outputType)
        {
            _text = text;
            _outputType = outputType;
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _text);
            translator.TranslateEnum(ref _outputType, (int)_outputType);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            ServerNodeConsoleWrite command = new();
            command.Translate(translator);

            return command;
        }
    }
}
