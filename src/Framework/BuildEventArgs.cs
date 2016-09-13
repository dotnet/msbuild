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
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public abstract class BuildEventArgs : EventArgs
    {
        /// <summary>
        /// Message
        /// </summary>
        private string message;

        /// <summary>
        /// Help keyword
        /// </summary>
        private string helpKeyword;

        /// <summary>
        /// Sender name
        /// </summary>
        private string senderName;

        /// <summary>
        /// Timestamp
        /// </summary>
        private DateTime timestamp;

        /// <summary>
        /// Thread id
        /// </summary>
        private int threadId;

        /// <summary>
        /// Build event context
        /// </summary>
#if FEATURE_BINARY_SERIALIZATION
        [OptionalField(VersionAdded = 2)]
#endif
        private BuildEventContext buildEventContext;

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
        /// <param name="eventTimestamp">TimeStamp of when the event was created</param>
        protected BuildEventArgs(string message, string helpKeyword, string senderName, DateTime eventTimestamp)
        {
            this.message = message;
            this.helpKeyword = helpKeyword;
            this.senderName = senderName;
            timestamp = eventTimestamp;
            threadId = System.Threading.Thread.CurrentThread.GetHashCode();
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
                if (timestamp.Kind == DateTimeKind.Utc)
                {
                    timestamp = timestamp.ToLocalTime();
                }

                return timestamp;
            }
        }

        /// <summary>
        /// The thread that raised event.  
        /// </summary>
        public int ThreadId
        {
            get
            {
                return threadId;
            }
        }

        /// <summary>
        /// Text of event. 
        /// </summary>
        public virtual string Message
        {
            get
            {
                return message;
            }

            protected set
            {
                message = value;
            }
        }

        /// <summary>
        /// Custom help keyword associated with event.
        /// </summary>
        public string HelpKeyword
        {
            get
            {
                return helpKeyword;
            }
        }

        /// <summary>
        /// Name of the object sending this event.
        /// </summary>
        public string SenderName
        {
            get
            {
                return senderName;
            }
        }

        /// <summary>
        /// Event contextual information for the build event argument
        /// </summary>
        public BuildEventContext BuildEventContext
        {
            get
            {
                return buildEventContext;
            }

            set
            {
                buildEventContext = value;
            }
        }

#if FEATURE_BINARY_SERIALIZATION
        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal virtual void WriteToStream(BinaryWriter writer)
        {
            #region Message
            if (message == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(message);
            }
            #endregion
            #region HelpKeyword
            if (helpKeyword == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(helpKeyword);
            }
            #endregion
            #region SenderName
            if (senderName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(senderName);
            }
            #endregion
            #region TimeStamp
            writer.Write((Int64)timestamp.Ticks);
            writer.Write((Int32)timestamp.Kind);
            #endregion
            writer.Write((Int32)threadId);
            #region BuildEventContext
            if (buildEventContext == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)buildEventContext.NodeId);
                writer.Write((Int32)buildEventContext.ProjectContextId);
                writer.Write((Int32)buildEventContext.TargetId);
                writer.Write((Int32)buildEventContext.TaskId);
                writer.Write((Int32)buildEventContext.SubmissionId);
                writer.Write((Int32)buildEventContext.ProjectInstanceId);
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
                message = null;
            }
            else
            {
                message = reader.ReadString();
            }
            #endregion
            #region HelpKeyword
            if (reader.ReadByte() == 0)
            {
                helpKeyword = null;
            }
            else
            {
                helpKeyword = reader.ReadString();
            }
            #endregion
            #region SenderName
            if (reader.ReadByte() == 0)
            {
                senderName = null;
            }
            else
            {
                senderName = reader.ReadString();
            }
            #endregion
            #region TimeStamp
            long timestampTicks = reader.ReadInt64();
            if (version > 20)
            {
                DateTimeKind kind = (DateTimeKind)reader.ReadInt32();
                timestamp = new DateTime(timestampTicks, kind);
            }
            else
            {
                timestamp = new DateTime(timestampTicks);
            }
            #endregion
            threadId = reader.ReadInt32();
            #region BuildEventContext
            if (reader.ReadByte() == 0)
            {
                buildEventContext = null;
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
                    buildEventContext = new BuildEventContext(submissionId, nodeId, projectInstanceId, projectContextId, targetId, taskId);
                }
                else
                {
                    buildEventContext = new BuildEventContext(nodeId, targetId, projectContextId, taskId);
                }
            }
            #endregion
        }
        #endregion
#endif

#if FEATURE_BINARY_SERIALIZATION
        #region SetSerializationDefaults
        /// <summary>
        /// Run before the object has been deserialized
        /// UNDONE (Logging.)  Can this and the next function go away, and instead return a BuildEventContext.Invalid from
        /// the property if the buildEventContext field is null?
        /// </summary>
        [OnDeserializing]
        private void SetBuildEventContextDefaultBeforeSerialization(StreamingContext sc)
        {
            // Don't want to create a new one here as default all the time as that would be a lot of 
            // possibly useless allocations
            buildEventContext = null;
        }

        /// <summary>
        /// Run after the object has been deserialized
        /// </summary>
        [OnDeserialized]
        private void SetBuildEventContextDefaultAfterSerialization(StreamingContext sc)
        {
            if (buildEventContext == null)
            {
                buildEventContext = new BuildEventContext
                                       (
                                       BuildEventContext.InvalidNodeId,
                                       BuildEventContext.InvalidTargetId,
                                       BuildEventContext.InvalidProjectContextId,
                                       BuildEventContext.InvalidTaskId
                                       );
            }
        }
        #endregion
#endif
    }
}
