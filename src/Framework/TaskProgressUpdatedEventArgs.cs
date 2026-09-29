// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the event raised when a task reports an intermediate progress update for one operation.
    /// </summary>
    [Serializable]
    public class TaskProgressUpdatedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes an instance of the <see cref="TaskProgressUpdatedEventArgs"/> class.
        /// </summary>
        public TaskProgressUpdatedEventArgs()
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="TaskProgressUpdatedEventArgs"/> class.
        /// </summary>
        /// <param name="operationId">The engine-generated identifier of the operation this update belongs to.</param>
        /// <param name="sequence">The engine-generated monotonically increasing sequence number of this update.</param>
        /// <param name="completed">The absolute amount of work completed.</param>
        /// <param name="total">The current total amount of work, or <see langword="null"/> when it is unknown.</param>
        /// <param name="status">An optional description of the operation's current activity.</param>
        /// <param name="helpKeyword">Help keyword.</param>
        /// <param name="senderName">The name of the sender of the event.</param>
        public TaskProgressUpdatedEventArgs(
            long operationId,
            long sequence,
            long completed,
            long? total,
            string? status,
            string? helpKeyword = null,
            string? senderName = null)
            : base(status, helpKeyword, senderName, MessageImportance.Low)
        {
            OperationId = operationId;
            Sequence = sequence;
            Completed = completed;
            Total = total;
            Status = status;
        }

        /// <summary>
        /// Gets or sets the engine-generated identifier of the operation this update belongs to.
        /// </summary>
        public long OperationId { get; set; }

        /// <summary>
        /// Gets or sets the engine-generated monotonically increasing sequence number of this update.
        /// </summary>
        public long Sequence { get; set; }

        /// <summary>
        /// Gets or sets the absolute amount of work completed.
        /// </summary>
        public long Completed { get; set; }

        /// <summary>
        /// Gets or sets the current total amount of work, or <see langword="null"/> when it is unknown.
        /// </summary>
        public long? Total { get; set; }

        /// <summary>
        /// Gets or sets an optional description of the operation's current activity.
        /// </summary>
        public string? Status { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(OperationId);
            writer.Write(Sequence);
            writer.Write(Completed);
            writer.WriteOptionalInt64(Total);
            writer.WriteOptionalString(Status);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            OperationId = reader.ReadInt64();
            Sequence = reader.ReadInt64();
            Completed = reader.ReadInt64();
            Total = reader.ReadOptionalInt64();
            Status = reader.ReadOptionalString();
        }
    }
}
