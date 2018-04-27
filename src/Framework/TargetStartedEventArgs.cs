// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for target started events
    /// </summary>
    /// <remarks>
    /// WARNING: marking a type [Serializable] without implementing
    /// ISerializable imposes a serialization contract -- it is a
    /// promise to never change the type's fields i.e. the type is
    /// immutable; adding new fields in the next version of the type
    /// without following certain special FX guidelines, can break both
    /// forward and backward compatibility
    /// </remarks>    
    [Serializable]
    public class TargetStartedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected TargetStartedEventArgs()
            : base()
        {
            // do nothing
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// Sender is assumed to be "MSBuild".
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="targetName">target name</param>
        /// <param name="projectFile">project file</param>
        /// <param name="targetFile">file in which the target is defined</param>
        public TargetStartedEventArgs
        (
            string message,
            string helpKeyword,
            string targetName,
            string projectFile,
            string targetFile
        )
            : this(message, helpKeyword, targetName, projectFile, targetFile, String.Empty, TargetBuiltReason.None, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// This constructor allows event data to be initialized including the timestamp when the event was created.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="targetName">target name</param>
        /// <param name="projectFile">project file</param>
        /// <param name="targetFile">file in which the target is defined</param>
        /// <param name="parentTarget">The part of the target.</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public TargetStartedEventArgs
        (
            string message,
            string helpKeyword,
            string targetName,
            string projectFile,
            string targetFile,
            string parentTarget,
            DateTime eventTimestamp
        )
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.targetName = targetName;
            this.projectFile = projectFile;
            this.targetFile = targetFile;
            this.parentTarget = parentTarget;
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="targetName">target name</param>
        /// <param name="projectFile">project file</param>
        /// <param name="targetFile">file in which the target is defined</param>
        /// <param name="parentTarget">The part of the target.</param>
        /// <param name="buildReason">The reason the parent built this target.</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public TargetStartedEventArgs
        (
            string message,
            string helpKeyword,
            string targetName,
            string projectFile,
            string targetFile,
            string parentTarget,
            TargetBuiltReason buildReason,
            DateTime eventTimestamp
        )
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.targetName = targetName;
            this.projectFile = projectFile;
            this.targetFile = targetFile;
            this.parentTarget = parentTarget;
            this.buildReason = buildReason;
        }

        private string targetName;
        private string projectFile;
        private string targetFile;
        private string parentTarget;
        private TargetBuiltReason buildReason;

        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.WriteOptionalString(targetName);
            writer.WriteOptionalString(projectFile);
            writer.WriteOptionalString(targetFile);
            writer.WriteOptionalString(parentTarget);

            writer.Write((int)buildReason);
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            targetName = reader.ReadByte() == 0 ? null : reader.ReadString();
            projectFile = reader.ReadByte() == 0 ? null : reader.ReadString();
            targetFile = reader.ReadByte() == 0 ? null : reader.ReadString();

            if (version > 20)
            {
                parentTarget = reader.ReadByte() == 0 ? null : reader.ReadString();
                buildReason = (TargetBuiltReason) reader.ReadInt32();
            }
        }
        #endregion

        /// <summary>
        /// target name
        /// </summary>
        public string TargetName => targetName;

        /// <summary>
        /// Target which caused this target to build
        /// </summary>
        public string ParentTarget => parentTarget;

        /// <summary>
        /// Project file associated with event.   
        /// </summary>
        public string ProjectFile => projectFile;

        /// <summary>
        /// File where this target was declared.
        /// </summary>
        public string TargetFile => targetFile;

        /// <summary>
        /// Why this target was built by its parent.
        /// </summary>
        public TargetBuiltReason BuildReason => buildReason;
    }
}
