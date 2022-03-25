// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.BackEnd
{
    internal class ServerNodeConsoleWrite : INodePacket
    {
        public string Text { get; }

        /// <summary>
        /// 1 = stdout, 2 = stderr
        /// </summary>
        public byte OutputType { get; }

        public ServerNodeConsoleWrite(string text, byte outputType)
        {
            Text = text;
            OutputType = outputType;
        }

        #region INodePacket Members

        /// <summary>
        /// Packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.ServerNodeConsole;

        #endregion

        public void Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var bw = translator.Writer;

                bw.Write(Text);
                bw.Write(OutputType);
            }
            else
            {
                throw new InvalidOperationException("Read from stream not supported");
            }
        }
    }
}
