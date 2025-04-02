// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This class encapsulates the default data associated with build events.
    /// It is intended to be extended/sub-classed.
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    [Serializable]
    public abstract class BuildEventArgs : EventArgs
    {
        /// <summary>
        /// Message. Volatile because it may be updated lock-free after construction.
        /// </summary>
        private volatile string? message;

        /// <summary>
        /// Help keyword
        /// </summary>
        private string? helpKeyword;

        /// <summary>
        /// Sender name
        /// </summary>
        private string? senderName;

        /// <summary>
        /// Timestamp
        /// </summary>
        private DateTime timestamp;

        [NonSerialized]
        private DateTime? _localTimestamp;

        /// <summary>
        /// Thread id
        /// </summary>
        private int threadId;

        /// <summary>
        /// Build event context
        /// </summary>
        [OptionalField(VersionAdded = 2)]
        private BuildEventContext? buildEventContext;

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
        protected BuildEventArgs(string? message, string? helpKeyword, string? senderName)
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
        protected BuildEventArgs(string? message, string? helpKeyword, string? senderName, DateTime eventTimestamp)
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
                if (!_localTimestamp.HasValue)
                {
                    _localTimestamp = timestamp.Kind == DateTimeKind.Utc || timestamp.Kind == DateTimeKind.Unspecified
                        ? timestamp.ToLocalTime()
                        : timestamp;
                }

                return _localTimestamp.Value;
            }
        }

        /// <summary>
        /// Exposes the private timestamp field to derived types.
        /// Used for serialization. Avoids the side effects of calling the
        /// <see cref="Timestamp"/> getter.
        /// </summary>
        protected internal DateTime RawTimestamp
        {
            get => timestamp;
            set => timestamp = value;
        }

        /// <summary>
        /// The thread that raised event.
        /// </summary>
        public int ThreadId => threadId;

        /// <summary>
        /// Text of event.
        /// </summary>
        public virtual string? Message
        {
            get => message;
            protected set => message = value;
        }

        /// <summary>
        /// Exposes the underlying message field without side-effects.
        /// Used for serialization.
        /// </summary>
        protected internal string? RawMessage
        {
            get => FormattedMessage;
            set => message = value;
        }

        /// <summary>
        /// Like <see cref="RawMessage"/> but returns a formatted message string if available.
        /// Used for serialization.
        /// </summary>
        private protected virtual string? FormattedMessage
        {
            get => message;
        }

        /// <summary>
        /// Custom help keyword associated with event.
        /// </summary>
        public string? HelpKeyword => helpKeyword;

        /// <summary>
        /// Name of the object sending this event.
        /// </summary>
        public string? SenderName => senderName;

        /// <summary>
        /// Event contextual information for the build event argument
        /// </summary>
        public BuildEventContext? BuildEventContext
        {
            get => buildEventContext;
            set => buildEventContext = value;
        }

        #region CustomSerializationToStream
        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        /// <param name="messageToWrite">The message to write to the stream.</param>
        private protected void WriteToStreamWithExplicitMessage(BinaryWriter writer, string? messageToWrite)
        {
            writer.WriteOptionalString(messageToWrite);
            writer.WriteOptionalString(helpKeyword);
            writer.WriteOptionalString(senderName);
            writer.WriteTimestamp(timestamp);
            writer.Write(threadId);
            writer.WriteOptionalBuildEventContext(buildEventContext);
        }

        /// <summary>
        /// Serializes to a stream through a binary writer
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into</param>
        internal virtual void WriteToStream(BinaryWriter writer)
        {
            WriteToStreamWithExplicitMessage(writer, RawMessage);
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal virtual void CreateFromStream(BinaryReader reader, int version)
        {
            message = reader.ReadOptionalString();
            helpKeyword = reader.ReadOptionalString();
            senderName = reader.ReadOptionalString();

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

            threadId = reader.ReadInt32();

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
                    int evaluationId = reader.ReadInt32();
                    buildEventContext = new BuildEventContext(submissionId, nodeId, evaluationId, projectInstanceId, projectContextId, targetId, taskId);
                }
                else
                {
                    buildEventContext = new BuildEventContext(nodeId, targetId, projectContextId, taskId);
                }
            }
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
                buildEventContext = BuildEventContext.Invalid;
            }
        }
        #endregion

        /// <summary>
        /// This is the default stub implementation, only here as a safeguard.
        /// Actual logic is injected from Microsoft.Build.dll to replace this.
        /// This is used by the Message property overrides to reconstruct the
        /// message lazily on demand.
        /// </summary>
        internal static Func<string, string?[], string> ResourceStringFormatter = (string resourceName, string?[] arguments) =>
        {
            var sb = new StringBuilder();
            sb.Append(resourceName);
            sb.Append('(');

            bool notFirst = false;
            foreach (var argument in arguments)
            {
                if (notFirst)
                {
                    sb.Append(',');
                }
                else
                {
                    notFirst = true;
                }

                sb.Append(argument);
            }

            sb.Append(')');
            return sb.ToString();
        };

        /// <summary>
        /// Shortcut method to mimic the original logic of creating the formatted strings.
        /// </summary>
        /// <param name="resourceName">Name of the resource string.</param>
        /// <param name="arguments">Optional list of arguments to pass to the formatted string.</param>
        /// <returns>The concatenated formatted string.</returns>
        internal static string FormatResourceStringIgnoreCodeAndKeyword(string resourceName, params string?[] arguments)
        {
            return ResourceStringFormatter(resourceName, arguments);
        }
    }
}
