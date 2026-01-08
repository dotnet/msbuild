// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The type of yield operation being requested.
    /// </summary>
    internal enum YieldOperation
    {
        /// <summary>
        /// Task is yielding control - non-blocking for the task.
        /// </summary>
        Yield = 0,

        /// <summary>
        /// Task is reacquiring control - blocking until acknowledged.
        /// </summary>
        Reacquire = 1,
    }

    /// <summary>
    /// Packet sent from TaskHost to parent for Yield/Reacquire operations.
    ///
    /// Yield/Reacquire flow:
    /// 1. Task calls Yield() → TaskHost sends YieldRequest(Yield) → returns immediately (non-blocking)
    /// 2. Task does non-build work...
    /// 3. Task calls Reacquire() → TaskHost sends YieldRequest(Reacquire) → blocks waiting for response
    /// 4. Parent receives YieldRequest(Reacquire) → calls IBuildEngine.Reacquire() (which may block)
    /// 5. When parent's Reacquire() returns → sends YieldResponse → TaskHost unblocks
    /// </summary>
    internal sealed class TaskHostYieldRequest : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private int _taskId;
        private YieldOperation _operation;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        private TaskHostYieldRequest()
        {
        }

        /// <summary>
        /// Constructor for creating a yield/reacquire request.
        /// </summary>
        /// <param name="taskId">The ID of the task that is yielding or reacquiring.</param>
        /// <param name="operation">The yield operation type.</param>
        public TaskHostYieldRequest(int taskId, YieldOperation operation)
        {
            _taskId = taskId;
            _operation = operation;
        }

        /// <summary>
        /// The packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.TaskHostYieldRequest;

        /// <summary>
        /// Request ID for correlation with response.
        /// </summary>
        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        /// <summary>
        /// The ID of the task that is yielding or reacquiring.
        /// </summary>
        public int TaskId => _taskId;

        /// <summary>
        /// The yield operation type.
        /// </summary>
        public YieldOperation Operation => _operation;

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostYieldRequest();
            packet.Translate(translator);
            return packet;
        }

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _taskId);
            translator.TranslateEnum(ref _operation, (int)_operation);
        }
    }
}
