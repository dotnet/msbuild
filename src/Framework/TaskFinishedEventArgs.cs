// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Event args for target finished event.</summary>
//-----------------------------------------------------------------------

using System.Runtime.InteropServices;
using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for task finished events
    /// </summary>
    /// <remarks>
    /// WARNING: marking a type [Serializable] without implementing
    /// ISerializable imposes a serialization contract -- it is a
    /// promise to never change the type's fields i.e. the type is
    /// immutable; adding new fields in the next version of the type
    /// without following certain special FX guidelines, can break both
    /// forward and backward compatibility
    /// </remarks>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
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
        public TaskFinishedEventArgs
        (
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            bool succeeded
        )
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
        public TaskFinishedEventArgs
        (
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            bool succeeded,
            DateTime eventTimestamp
        )
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

#if FEATURE_BINARY_SERIALIZATION
        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            #region TaskName
            if (taskName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(taskName);
            }
            #endregion
            #region ProjectFile
            if (projectFile == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(projectFile);
            }
            #endregion
            #region TaskFile
            if (taskFile == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(taskFile);
            }
            #endregion
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
            #region TaskName
            if (reader.ReadByte() == 0)
            {
                taskName = null;
            }
            else
            {
                taskName = reader.ReadString();
            }
            #endregion
            #region ProjectFile
            if (reader.ReadByte() == 0)
            {
                projectFile = null;
            }
            else
            {
                projectFile = reader.ReadString();
            }
            #endregion
            #region TaskFile
            if (reader.ReadByte() == 0)
            {
                taskFile = null;
            }
            else
            {
                taskFile = reader.ReadString();
            }
            #endregion
            succeeded = reader.ReadBoolean();
        }
        #endregion
#endif

        /// <summary>
        /// Task Name
        /// </summary>
        public string TaskName
        {
            get
            {
                return taskName;
            }
        }

        /// <summary>
        /// True if target built successfully, false otherwise
        /// </summary>
        public bool Succeeded
        {
            get
            {
                return succeeded;
            }
        }

        /// <summary>
        /// Project file associated with event.   
        /// </summary>
        public string ProjectFile
        {
            get
            {
                return projectFile;
            }
        }

        /// <summary>
        /// MSBuild file where this task was defined.   
        /// </summary>
        public string TaskFile
        {
            get
            {
                return taskFile;
            }
        }
    }
}
