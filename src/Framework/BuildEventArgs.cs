// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Event args for any build event.</summary>
//-----------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class encapsulates the default data associated with build events. 
    /// It is intended to be extended/sub-classed.
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
    public abstract class BuildEventArgs : EventArgs
    {
        /// <summary>
        /// Message
        /// </summary>
        private string _message;

        /// <summary>
        /// Help keyword
        /// </summary>
        private string _helpKeyword;

        /// <summary>
        /// Sender name
        /// </summary>
        private string _senderName;

        /// <summary>
        /// Timestamp
        /// </summary>
        private DateTime _timestamp;

        /// <summary>
        /// Thread id
        /// </summary>
        private int _threadId;

        /// <summary>
        /// Build event context
        /// </summary>
        [OptionalField(VersionAdded = 2)]
        private BuildEventContext _buildEventContext;

        /// <summary>
        /// Default constructor
        /// </summary>
        protected BuildEventArgs()
            : this(null, null, null, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// This constructor allows all event data to be initialized
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of event sender</param>
        protected BuildEventArgs(string message, string helpKeyword, string senderName)
            : this(message, helpKeyword, senderName, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// This constructor allows all event data to be initialized while providing a custom timestamp.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword </param>
        /// <param name="senderName">name of event sender</param>
        /// <param name="eventTimeStamp">TimeStamp of when the event was created</param>
        protected BuildEventArgs(string message, string helpKeyword, string senderName, DateTime eventTimestamp)
        {
            _message = message;
            _helpKeyword = helpKeyword;
            _senderName = senderName;
            _timestamp = eventTimestamp;
            _threadId = System.Threading.Thread.CurrentThread.GetHashCode();
        }

        /// <summary>
        /// The time when event was raised.
        /// </summary>
        public DateTime Timestamp
        {
            get
            {
                // Rather than storing dates in Local time all the time, we store in UTC type, and only
                // convert to Local when the user requests access to this field.  This lets us avoid the
                // expensive conversion to Local time unless it's absolutely necessary.
                if (_timestamp.Kind == DateTimeKind.Utc)
                {
                    _timestamp = _timestamp.ToLocalTime();
                }

                return _timestamp;
            }
        }

        /// <summary>
        /// The thread that raised event.  
        /// </summary>
        public int ThreadId
        {
            get
            {
                return _threadId;
            }
        }

        /// <summary>
        /// Text of event. 
        /// </summary>
        public virtual string Message
        {
            get
            {
                return _message;
            }

            protected set
            {
                _message = value;
            }
        }

        /// <summary>
        /// Custom help keyword associated with event.
        /// </summary>
        public string HelpKeyword
        {
            get
            {
                return _helpKeyword;
            }
        }

        /// <summary>
        /// Name of the object sending this event.
        /// </summary>
        public string SenderName
        {
            get
            {
                return _senderName;
            }
        }

        /// <summary>
        /// Event contextual information for the build event argument
        /// </summary>
        public BuildEventContext BuildEventContext
        {
            get
            {
                return _buildEventContext;
            }

            set
            {
                _buildEventContext = value;
            }
        }

        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal virtual void WriteToStream(BinaryWriter writer)
        {
            #region Message
            if (_message == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(_message);
            }
            #endregion
            #region HelpKeyword
            if (_helpKeyword == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(_helpKeyword);
            }
            #endregion
            #region SenderName
            if (_senderName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(_senderName);
            }
            #endregion
            #region TimeStamp
            writer.Write((Int64)_timestamp.Ticks);
            writer.Write((Int32)_timestamp.Kind);
            #endregion
            writer.Write((Int32)_threadId);
            #region BuildEventContext
            if (_buildEventContext == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)_buildEventContext.NodeId);
                writer.Write((Int32)_buildEventContext.ProjectContextId);
                writer.Write((Int32)_buildEventContext.TargetId);
                writer.Write((Int32)_buildEventContext.TaskId);
                writer.Write((Int32)_buildEventContext.SubmissionId);
                writer.Write((Int32)_buildEventContext.ProjectInstanceId);
            }
            #endregion
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal virtual void CreateFromStream(BinaryReader reader, int version)
        {
            #region Message
            if (reader.ReadByte() == 0)
            {
                _message = null;
            }
            else
            {
                _message = reader.ReadString();
            }
            #endregion
            #region HelpKeyword
            if (reader.ReadByte() == 0)
            {
                _helpKeyword = null;
            }
            else
            {
                _helpKeyword = reader.ReadString();
            }
            #endregion
            #region SenderName
            if (reader.ReadByte() == 0)
            {
                _senderName = null;
            }
            else
            {
                _senderName = reader.ReadString();
            }
            #endregion
            #region TimeStamp
            long timestampTicks = reader.ReadInt64();
            if (version > 20)
            {
                DateTimeKind kind = (DateTimeKind)reader.ReadInt32();
                _timestamp = new DateTime(timestampTicks, kind);
            }
            else
            {
                _timestamp = new DateTime(timestampTicks);
            }
            #endregion
            _threadId = reader.ReadInt32();
            #region BuildEventContext
            if (reader.ReadByte() == 0)
            {
                _buildEventContext = null;
            }
            else
            {
                int nodeId = reader.ReadInt32();
                int projectContextId = reader.ReadInt32();
                int targetId = reader.ReadInt32();
                int taskId = reader.ReadInt32();

                if (version > 20)
                {
                    int submissionId = reader.ReadInt32();
                    int projectInstanceId = reader.ReadInt32();
                    _buildEventContext = new BuildEventContext(submissionId, nodeId, projectInstanceId, projectContextId, targetId, taskId);
                }
                else
                {
                    _buildEventContext = new BuildEventContext(nodeId, targetId, projectContextId, taskId);
                }
            }
            #endregion
        }
        #endregion
        #region SetSerializationDefaults

        /// <summary>
        /// Run before the object has been deserialized
        /// UNDONE (Logging.)  Can this and the next function go away, and instead return a BuildEventContext.Invalid from
        /// the property if the buildEventContext field is null?
        /// </summary>
        [OnDeserializing]
        private void SetBuildEventContextDefaultBeforeSerialization(StreamingContext sc)
        {
            // Dont want to create a new one here as default all the time as that would be a lot of 
            // possibly useless allocations
            _buildEventContext = null;
        }

        /// <summary>
        /// Run after the object has been deserialized
        /// </summary>
        [OnDeserialized]
        private void SetBuildEventContextDefaultAfterSerialization(StreamingContext sc)
        {
            if (_buildEventContext == null)
            {
                _buildEventContext = new BuildEventContext
                                       (
                                       BuildEventContext.InvalidNodeId,
                                       BuildEventContext.InvalidTargetId,
                                       BuildEventContext.InvalidProjectContextId,
                                       BuildEventContext.InvalidTaskId
                                       );
            }
        }
        #endregion
    }
}

