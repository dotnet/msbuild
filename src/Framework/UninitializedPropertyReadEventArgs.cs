// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// The arguments for an uninitialized property read event.
    /// </summary>
    [Serializable]
    public class UninitializedPropertyReadEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// UninitializedPropertyReadEventArgs
        /// </summary>
        public UninitializedPropertyReadEventArgs()
        {
        }

        /// <summary>
        /// Creates an instance of the UninitializedPropertyReadEventArgs class
        /// </summary>
        /// <param name="propertyName">The name of the uninitialized property that was read.</param>
        /// <param name="message">The message of the uninitialized property that was read.</param>
        /// <param name="helpKeyword">The helpKeyword of the uninitialized property that was read.</param>
        /// <param name="senderName">The sender name of the event.</param>
        /// <param name="importance">The message importance of the event.</param>
        public UninitializedPropertyReadEventArgs(
            string propertyName,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low) : base(message, helpKeyword, senderName, importance)
        {
            this.PropertyName = propertyName;
        }

        /// <summary>
        /// The name of the uninitialized property that was read.
        /// </summary>
        public string PropertyName { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(PropertyName);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            PropertyName = reader.ReadOptionalString();
        }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword("UninitializedPropertyRead", PropertyName);
                }

                return RawMessage;
            }
        }
    }
}
