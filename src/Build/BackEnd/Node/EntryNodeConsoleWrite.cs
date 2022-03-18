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

        public EntryNodeConsoleWrite(string text, byte outputType)
        {
            Text = text;
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
}
