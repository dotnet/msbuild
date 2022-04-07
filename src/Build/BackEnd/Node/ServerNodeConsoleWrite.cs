// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    internal sealed class ServerNodeConsoleWrite : INodePacket
    {
        private string _text = default!;
        private byte _outputType = default!;

        public string Text => _text;

        /// <summary>
        /// 1 = stdout, 2 = stderr
        /// </summary>
        public byte OutputType => _outputType;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeConsoleWrite() { }

        public ServerNodeConsoleWrite(string text, byte outputType)
        {
            _text = text;
            _outputType = outputType;
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeConsole;

        #endregion

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _text);
            translator.Translate(ref _outputType);
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            ServerNodeConsoleWrite command = new();
            command.Translate(translator);

            return command;
        }
    }
}
