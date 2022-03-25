// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace Microsoft.Build.BackEnd
{
    internal class ServerNodeResponse : INodePacket
    {
        public ServerNodeResponse(int exitCode, string exitType)
        {
            ExitCode = exitCode;
            ExitType = exitType;
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// This has to be in sync with Microsoft.Build.BackEnd.NodePacketType.ServerNodeBuildCommand
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeResponse;

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
}
