// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Event args for task started event.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for task started events
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
        public TaskStartedEventArgs
        (
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName
        )
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
        /// <param name="eventTimestamp">Timestamp when event was created</param>
        public TaskStartedEventArgs
        (
            string message,
            string helpKeyword,
            string projectFile,
            string taskFile,
            string taskName,
            DateTime eventTimestamp
        )
            : base(message, helpKeyword, "MSBuild", eventTimestamp)
        {
            _taskName = taskName;
            _projectFile = projectFile;
            _taskFile = taskFile;
        }

        private string _taskName;
        private string _projectFile;
        private string _taskFile;

        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            #region TaskName
            if (_taskName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(_taskName);
            }
            #endregion
            #region ProjectFile
            if (_projectFile == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(_projectFile);
            }
            #endregion
            #region TaskFile
            if (_taskFile == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(_taskFile);
            }
            #endregion
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
                _taskName = null;
            }
            else
            {
                _taskName = reader.ReadString();
            }
            #endregion
            #region ProjectFile
            if (reader.ReadByte() == 0)
            {
                _projectFile = null;
            }
            else
            {
                _projectFile = reader.ReadString();
            }
            #endregion
            #region TaskFile
            if (reader.ReadByte() == 0)
            {
                _taskFile = null;
            }
            else
            {
                _taskFile = reader.ReadString();
            }
            #endregion
        }
        #endregion

        /// <summary>
        /// Task name.
        /// </summary>
        public string TaskName
        {
            get
            {
                return _taskName;
            }
        }

        /// <summary>
        /// Project file associated with event.   
        /// </summary>
        public string ProjectFile
        {
            get
            {
                return _projectFile;
            }
        }

        /// <summary>
        /// MSBuild file where this task was defined.   
        /// </summary>
        public string TaskFile
        {
            get
            {
                return _taskFile;
            }
        }
    }
}
