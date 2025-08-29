﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for task finished events
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    [Serializable]
    public class TaskFinishedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected TaskFinishedEventArgs()
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
        /// <param name="projectFile">project file</param>
        /// <param name="taskFile">file in which the task is defined</param>
        /// <param name="taskName">task name</param> 
        /// <param name="succeeded">true indicates task succeed</param>
        public TaskFinishedEventArgs(
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            bool succeeded)
            : this(message, helpKeyword, projectFile, taskFile, taskName, succeeded, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// This constructor allows event data to be initialized and the timestamp to be set
        /// Sender is assumed to be "MSBuild".
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="projectFile">project file</param>
        /// <param name="taskFile">file in which the task is defined</param>
        /// <param name="taskName">task name</param> 
        /// <param name="succeeded">true indicates task succeed</param>
        /// <param name="eventTimestamp">Timestamp when event was created</param>
        public TaskFinishedEventArgs(
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            bool succeeded,
            DateTime eventTimestamp)
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.taskName = taskName;
            this.taskFile = taskFile;
            this.succeeded = succeeded;
            this.projectFile = projectFile;
        }

        private string taskName;
        private string projectFile;
        private string taskFile;
        private bool succeeded;

        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(taskName);
            writer.WriteOptionalString(projectFile);
            writer.WriteOptionalString(taskFile);

            writer.Write(succeeded);
        }

        /// <summary>
        /// Deserializes the Errorevent from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            taskName = reader.ReadByte() == 0 ? null : reader.ReadString();
            projectFile = reader.ReadByte() == 0 ? null : reader.ReadString();
            taskFile = reader.ReadByte() == 0 ? null : reader.ReadString();

            succeeded = reader.ReadBoolean();
        }
        #endregion

        /// <summary>
        /// Task Name
        /// </summary>
        public string TaskName => taskName;

        /// <summary>
        /// True if target built successfully, false otherwise
        /// </summary>
        public bool Succeeded => succeeded;

        /// <summary>
        /// Project file associated with event.   
        /// </summary>
        public string ProjectFile => projectFile;

        /// <summary>
        /// MSBuild file where this task was defined.   
        /// </summary>
        public string TaskFile => taskFile;

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword(Succeeded ? "TaskFinishedSuccess" : "TaskFinishedFailure", TaskName);
                }

                return RawMessage;
            }
        }
    }
}
