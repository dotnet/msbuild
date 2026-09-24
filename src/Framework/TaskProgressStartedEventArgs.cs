// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the event raised when a task begins reporting progress for one operation.
    /// </summary>
    [Serializable]
    public class TaskProgressStartedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes an instance of the <see cref="TaskProgressStartedEventArgs"/> class.
        /// </summary>
        public TaskProgressStartedEventArgs()
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="TaskProgressStartedEventArgs"/> class.
        /// </summary>
        /// <param name="operationId">The engine-generated identifier for this operation, unique within the build.</param>
        /// <param name="title">A short, stable description of the operation.</param>
        /// <param name="unit">The unit that progress values are expressed in.</param>
        /// <param name="helpKeyword">Help keyword.</param>
        /// <param name="senderName">The name of the sender of the event.</param>
        public TaskProgressStartedEventArgs(
            long operationId,
            string title,
            TaskProgressUnit unit,
            string? helpKeyword = null,
            string? senderName = null)
            : base(title, helpKeyword, senderName, MessageImportance.Low)
        {
            OperationId = operationId;
            Title = title;
            Unit = unit;
        }

        /// <summary>
        /// Gets or sets the engine-generated identifier for this operation, unique within the build.
        /// </summary>
        public long OperationId { get; set; }

        /// <summary>
        /// Gets or sets the short, stable description of the operation.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the unit that progress values are expressed in.
        /// </summary>
        public TaskProgressUnit Unit { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(OperationId);
            writer.WriteOptionalString(Title);
            writer.Write((int)Unit);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            OperationId = reader.ReadInt64();
            Title = reader.ReadOptionalString();
            Unit = (TaskProgressUnit)reader.ReadInt32();
        }
    }
}
