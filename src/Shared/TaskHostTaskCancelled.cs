// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// TaskHostTaskCancelled informs the task host that the task it is
    /// currently executing has been canceled.
    /// </summary>
    internal class TaskHostTaskCancelled : INodePacket
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public TaskHostTaskCancelled()
        {
        }

        /// <summary>
        /// The type of this NodePacket
        /// </summary>
        public NodePacketType Type
        {
            get { return NodePacketType.TaskHostTaskCancelled; }
        }

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        /// <param name="translator">The translator to use.</param>
        public void Translate(ITranslator translator)
        {
            // Do nothing -- this packet doesn't contain any parameters.
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            TaskHostTaskCancelled taskCancelled = new TaskHostTaskCancelled();
            taskCancelled.Translate(translator);
            return taskCancelled;
        }
    }
}
