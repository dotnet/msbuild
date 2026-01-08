// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Response packet from parent to TaskHost for yield/reacquire operations.
    ///
    /// This packet is sent by the parent after it has processed a Reacquire request.
    /// The parent's IBuildEngine.Reacquire() call may block waiting for the scheduler
    /// to allow the task to continue. Once that returns, this response is sent to
    /// unblock the TaskHost's Reacquire() call.
    ///
    /// Note: Yield requests do NOT receive a response - they are fire-and-forget.
    /// Only Reacquire requests block and wait for this response.
    /// </summary>
    internal sealed class TaskHostYieldResponse : INodePacket, ITaskHostCallbackPacket
    {
        private int _requestId;
        private bool _success;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        private TaskHostYieldResponse()
        {
        }

        /// <summary>
        /// Constructor for creating a response.
        /// </summary>
        /// <param name="requestId">The ID of the request this is responding to.</param>
        /// <param name="success">Whether the reacquire was successful.</param>
        public TaskHostYieldResponse(int requestId, bool success)
        {
            _requestId = requestId;
            _success = success;
        }

        /// <summary>
        /// The packet type.
        /// </summary>
        public NodePacketType Type => NodePacketType.TaskHostYieldResponse;

        /// <summary>
        /// The request ID this response corresponds to.
        /// </summary>
        public int RequestId
        {
            get => _requestId;
            set => _requestId = value;
        }

        /// <summary>
        /// Whether the reacquire was successful.
        /// </summary>
        public bool Success => _success;

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            var packet = new TaskHostYieldResponse();
            packet.Translate(translator);
            return packet;
        }

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _requestId);
            translator.Translate(ref _success);
        }
    }
}
