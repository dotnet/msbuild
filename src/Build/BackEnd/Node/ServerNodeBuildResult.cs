// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

namespace Microsoft.Build.BackEnd
{
    internal sealed class ServerNodeBuildResult : INodePacket
    {
        private int _exitCode = default!;
        private string _exitType = default!;

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private ServerNodeBuildResult() { }

        public ServerNodeBuildResult(int exitCode, string exitType)
        {
            _exitCode = exitCode;
            _exitType = exitType;
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.ServerNodeBuildCommand
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeBuildResult;

        #endregion

        public int ExitCode => _exitCode;

        public string ExitType => _exitType;

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
