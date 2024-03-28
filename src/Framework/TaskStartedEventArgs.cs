﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for task started events
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    [Serializable]
    public class TaskStartedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected TaskStartedEventArgs()
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
        public TaskStartedEventArgs(
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName)
            : this(message, helpKeyword, projectFile, taskFile, taskName, DateTime.UtcNow)
        {
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
        /// <param name="taskAssemblyName">An assembly's unique identity where the task is implemented</param>
        public TaskStartedEventArgs(
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            AssemblyName taskAssemblyName)
            : this(message, helpKeyword, projectFile, taskFile, taskName, DateTime.UtcNow, taskAssemblyName)
        {
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
        /// <param name="eventTimestamp">Timestamp when event was created</param>
        public TaskStartedEventArgs(
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            DateTime eventTimestamp)
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.taskName = taskName;
            this.projectFile = projectFile;
            this.taskFile = taskFile;
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
        /// <param name="eventTimestamp">Timestamp when event was created</param>
        /// <param name="taskAssemblyName">An assembly's unique identity where the task is implemented</param>
        public TaskStartedEventArgs(
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            DateTime eventTimestamp,
            AssemblyName taskAssemblyName)
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            this.taskName = taskName;
            this.projectFile = projectFile;
            this.taskFile = taskFile;
            TaskAssemblyName = taskAssemblyName;
        }
        
        private string taskName;
        private string projectFile;
        private string taskFile;

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
            writer.Write7BitEncodedInt(LineNumber);
            writer.Write7BitEncodedInt(ColumnNumber);
            writer.WriteOptionalString(TaskAssemblyName?.FullName);
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
            LineNumber = reader.Read7BitEncodedInt();
            ColumnNumber = reader.Read7BitEncodedInt();
            TaskAssemblyName = reader.ReadByte() == 0 ? null : new AssemblyName(reader.ReadString());
        }
        #endregion

        /// <summary>
        /// Task name.
        /// </summary>
        public string TaskName => taskName;

        /// <summary>
        /// Project file associated with event.
        /// </summary>
        public string ProjectFile => projectFile;

        /// <summary>
        /// MSBuild file where this task was defined.
        /// </summary>
        public string TaskFile => taskFile;

        /// <summary>
        /// Line number of the task invocation in the project file
        /// </summary>
        public int LineNumber { get; internal set; }

        /// <summary>
        /// Column number of the task invocation in the project file
        /// </summary>
        public int ColumnNumber { get; internal set; }

        /// <summary>
        /// Full name of the assembly that implements the task
        /// </summary>
        public AssemblyName TaskAssemblyName { get; private set; }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword("TaskStarted", TaskName);
                }

                return RawMessage;
            }
        }
    }
}
