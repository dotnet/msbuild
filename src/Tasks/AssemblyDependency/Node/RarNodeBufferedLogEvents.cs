// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    /// <summary>
    /// Represents a queue of log events emitted from the out-of-proc RAR task, for replay by the client.
    /// </summary>
    internal class RarNodeBufferedLogEvents : INodePacket
    {
        private List<LogMessagePacketBase> _eventQueue = [];

        internal RarNodeBufferedLogEvents(int capacity) => _eventQueue = new(capacity);

        internal RarNodeBufferedLogEvents(ITranslator translator) => Translate(translator);

        public NodePacketType Type => NodePacketType.RarNodeBufferedLogEvents;

        internal List<LogMessagePacketBase> EventQueue => _eventQueue;

        public void Translate(ITranslator translator)
            => translator.Translate(ref _eventQueue, static t => new LogMessagePacketBase(t));
    }
}
