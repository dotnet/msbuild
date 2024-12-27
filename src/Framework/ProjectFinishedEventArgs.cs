// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    public class ProjectBuildStats
    {
        public short TotalTasksCount { get; set; }
        public short CustomTasksCount { get; set; }
        public short TotalExecutedTasksCount { get; set; }
        public short ExecutedCustomTasksCount { get; set; }
        public TimeSpan TotalTasksExecution { get; set; }
        public TimeSpan TotalCustomTasksExecution { get; set; }

        // todo top N tasks - names (unhashed if not custom) and time
    }

    /// <summary>
    /// Arguments for project finished events
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    [Serializable]
    public class ProjectFinishedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected ProjectFinishedEventArgs()
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
        /// <param name="projectFile">name of the project</param>
        /// <param name="succeeded">true indicates project built successfully</param>
        public ProjectFinishedEventArgs(
            string? message,
            string? helpKeyword,
            string? projectFile,
            bool succeeded)
            : this(message, helpKeyword, projectFile, succeeded, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// This constructor allows event data to be initialized.
        /// Sender is assumed to be "MSBuild". This constructor allows the timestamp to be set as well
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="projectFile">name of the project</param>
        /// <param name="succeeded">true indicates project built successfully</param>
        /// <param name="eventTimestamp">Timestamp when the event was created</param>
        public ProjectFinishedEventArgs(
            string? message,
            string? helpKeyword,
            string? projectFile,
            bool succeeded,
            DateTime eventTimestamp)
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.projectFile = projectFile;
            this.succeeded = succeeded;
        }

        private string? projectFile;
        private bool succeeded;

        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(projectFile);
            writer.Write(succeeded);

            if (ProjectBuildStats != null)
            {
                writer.Write((byte)1);
                writer.Write(ProjectBuildStats.TotalTasksCount);
                writer.Write(ProjectBuildStats.CustomTasksCount);
                writer.Write(ProjectBuildStats.TotalExecutedTasksCount);
                writer.Write(ProjectBuildStats.ExecutedCustomTasksCount);

                writer.Write(ProjectBuildStats.TotalTasksExecution.Ticks);
                writer.Write(ProjectBuildStats.TotalCustomTasksExecution.Ticks);
            }
            else
            {
                writer.Write((byte)0);
            }
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            projectFile = reader.ReadByte() == 0 ? null : reader.ReadString();
            succeeded = reader.ReadBoolean();

            if (reader.ReadByte() == 1)
            {
                ProjectBuildStats = new ProjectBuildStats()
                {
                    TotalTasksCount = reader.ReadInt16(),
                    CustomTasksCount = reader.ReadInt16(),
                    TotalExecutedTasksCount = reader.ReadInt16(),
                    ExecutedCustomTasksCount = reader.ReadInt16(),
                    TotalTasksExecution = TimeSpan.FromTicks(reader.ReadInt64()),
                    TotalCustomTasksExecution = TimeSpan.FromTicks(reader.ReadInt64()),
                };
            }
        }
        #endregion

        /// <summary>
        /// Project name
        /// </summary>
        public string? ProjectFile => projectFile;

        /// <summary>
        /// True if project built successfully, false otherwise
        /// </summary>
        public bool Succeeded => succeeded;

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword(Succeeded ? "ProjectFinishedSuccess" : "ProjectFinishedFailure", Path.GetFileName(ProjectFile));
                }

                return RawMessage;
            }
        }

        // public int Foo1 { get; set; }

        public ProjectBuildStats? ProjectBuildStats { get; set; }
    }
}
