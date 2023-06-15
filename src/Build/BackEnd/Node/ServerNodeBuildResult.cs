// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    internal sealed class ServerNodeBuildResult : INodePacket
    {
        private int _exitCode = default!;
        private string _exitType = default!;

        /// <summary>
        /// Packet type.
        /// This has to be in sync with <see cref="NodePacketType.ServerNodeBuildResult" />
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeBuildResult;

        public int ExitCode => _exitCode;

        public string ExitType => _exitType;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeBuildResult() { }

        public ServerNodeBuildResult(int exitCode, string exitType)
        {
            _exitCode = exitCode;
            _exitType = exitType;
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _exitCode);
            translator.Translate(ref _exitType);
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            ServerNodeBuildResult command = new();
            command.Translate(translator);

            return command;
        }
    }
}
