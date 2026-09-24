// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the event raised when a task's progress operation reaches a terminal state.
    /// </summary>
    [Serializable]
    public class TaskProgressFinishedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes an instance of the <see cref="TaskProgressFinishedEventArgs"/> class.
        /// </summary>
        public TaskProgressFinishedEventArgs()
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="TaskProgressFinishedEventArgs"/> class.
        /// </summary>
        /// <param name="operationId">The engine-generated identifier of the operation that ended.</param>
        /// <param name="sequence">The engine-generated monotonically increasing sequence number of this event.</param>
        /// <param name="outcome">How the operation ended.</param>
        /// <param name="completed">The final absolute amount of work completed.</param>
        /// <param name="total">The final total amount of work, or <see langword="null"/> when it is unknown.</param>
        /// <param name="summary">An optional final summary.</param>
        /// <param name="helpKeyword">Help keyword.</param>
        /// <param name="senderName">The name of the sender of the event.</param>
        public TaskProgressFinishedEventArgs(
            long operationId,
            long sequence,
            TaskProgressOutcome outcome,
            long completed,
            long? total,
            string? summary,
            string? helpKeyword = null,
            string? senderName = null)
            : base(summary, helpKeyword, senderName, MessageImportance.Low)
        {
            OperationId = operationId;
            Sequence = sequence;
            Outcome = outcome;
            Completed = completed;
            Total = total;
            Summary = summary;
        }

        /// <summary>
        /// Gets or sets the engine-generated identifier of the operation that ended.
        /// </summary>
        public long OperationId { get; set; }

        /// <summary>
        /// Gets or sets the engine-generated monotonically increasing sequence number of this event.
        /// </summary>
        public long Sequence { get; set; }

        /// <summary>
        /// Gets or sets how the operation ended.
        /// </summary>
        public TaskProgressOutcome Outcome { get; set; }

        /// <summary>
        /// Gets or sets the final absolute amount of work completed.
        /// </summary>
        public long Completed { get; set; }

        /// <summary>
        /// Gets or sets the final total amount of work, or <see langword="null"/> when it is unknown.
        /// </summary>
        public long? Total { get; set; }

        /// <summary>
        /// Gets or sets an optional final summary.
        /// </summary>
        public string? Summary { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(OperationId);
            writer.Write(Sequence);
            writer.Write((int)Outcome);
            writer.Write(Completed);
            writer.WriteOptionalInt64(Total);
            writer.WriteOptionalString(Summary);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            OperationId = reader.ReadInt64();
            Sequence = reader.ReadInt64();
            Outcome = (TaskProgressOutcome)reader.ReadInt32();
            Completed = reader.ReadInt64();
            Total = reader.ReadOptionalInt64();
            Summary = reader.ReadOptionalString();
        }
    }
}
