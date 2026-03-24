// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Specifies which yield operation is being requested.
    /// </summary>
    internal enum YieldOperation : byte
    {
        /// <summary>
        /// The task is yielding the node, allowing other work to be scheduled.
        /// Fire-and-forget: no response is sent.
        /// </summary>
        Yield = 0,

        /// <summary>
        /// The task is reacquiring the node after a yield.
        /// Blocking: the TaskHost waits for a <see cref="TaskHostYieldResponse"/> before continuing.
        /// </summary>
        Reacquire = 1,
    }

    /// <summary>
    /// Packet sent from TaskHost to owning worker node for Yield/Reacquire operations.
    /// <para>
    /// Yield is fire-and-forget (no response expected).
    /// Reacquire blocks until a <see cref="TaskHostYieldResponse"/> is received.
    /// </para>
    /// </summary>
    internal class TaskHostYieldRequest : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private YieldOperation _operation;

        public TaskHostYieldRequest()
        {
        }

        public TaskHostYieldRequest(YieldOperation operation)
        {
            _operation = operation;
        }

        public NodePacketType Type => NodePacketType.TaskHostYieldRequest;

        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        public YieldOperation Operation => _operation;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);

            byte op = (byte)_operation;
            translator.Translate(ref op);
            _operation = (YieldOperation)op;
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostYieldRequest();
            packet.Translate(translator);
            return packet;
        }
    }
}
