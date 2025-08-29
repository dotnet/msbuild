﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// The argument for a property reassignment event.
    /// </summary>
    [Serializable]
    public class PropertyReassignmentEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Creates an instance of the PropertyReassignmentEventArgs class.
        /// </summary>
        public PropertyReassignmentEventArgs()
        {
        }

        /// <summary>
        /// Creates an instance of the PropertyReassignmentEventArgs class.
        /// </summary>
        /// <param name="propertyName">The name of the property whose value was reassigned.</param>
        /// <param name="previousValue">The previous value of the reassigned property.</param>
        /// <param name="newValue">The new value of the reassigned property.</param>
        /// <param name="location">The location of the reassignment.</param>
        /// <param name="message">The message of the reassignment event.</param>
        /// <param name="helpKeyword">The help keyword of the reassignment.</param>
        /// <param name="senderName">The sender name of the reassignment event.</param>
        /// <param name="importance">The importance of the message.</param>
        public PropertyReassignmentEventArgs(
            string propertyName,
            string previousValue,
            string newValue,
            string location,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low) : base(message, helpKeyword, senderName, importance)
        {
            this.PropertyName = propertyName;
            this.PreviousValue = previousValue;
            this.NewValue = newValue;
            this.Location = location;
        }

        /// <summary>
        /// The name of the property whose value was reassigned.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// The previous value of the reassigned property.
        /// </summary>
        public string PreviousValue { get; set; }

        /// <summary>
        /// The new value of the reassigned property.
        /// </summary>
        public string NewValue { get; set; }

        /// <summary>
        /// The location of the reassignment.
        /// </summary>
        public string Location { get; set; }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword("PropertyReassignment", PropertyName, NewValue, PreviousValue, Location);
                }

                return RawMessage;
            }
        }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(PropertyName);
            writer.WriteOptionalString(NewValue);
            writer.WriteOptionalString(PreviousValue);
            writer.WriteOptionalString(Location);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            PropertyName = reader.ReadOptionalString();
            NewValue = reader.ReadOptionalString();
            PreviousValue = reader.ReadOptionalString();
            Location = reader.ReadOptionalString();
        }
    }
}
