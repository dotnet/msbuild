// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// The argument for a property initial value set event.
    /// </summary>
    [Serializable]
    public class PropertyInitialValueSetEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Creates an instance of the <see cref="PropertyInitialValueSetEventArgs"/> class.
        /// </summary>
        public PropertyInitialValueSetEventArgs() { }

        /// <summary>
        /// Creates an instance of the <see cref="PropertyInitialValueSetEventArgs"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="propertyValue">The value of the property.</param>
        /// <param name="propertySource">The source of the property.</param>
        /// <param name="message">The message of the property.</param>
        /// <param name="helpKeyword">The help keyword.</param>
        /// <param name="senderName">The sender name of the event.</param>
        /// <param name="importance">The importance of the message.</param>
        public PropertyInitialValueSetEventArgs(
            string propertyName,
            string propertyValue,
            string propertySource,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low) : base(message, helpKeyword, senderName, importance)
        {
            this.PropertyName = propertyName;
            this.PropertyValue = propertyValue;
            this.PropertySource = propertySource;
        }

        /// <summary>
        /// The name of the property.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// The value of the property.
        /// </summary>
        public string PropertyValue { get; set; }

        /// <summary>
        /// The source of the property.
        /// </summary>
        public string PropertySource { get; set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(PropertyName);
            writer.WriteOptionalString(PropertyValue);
            writer.WriteOptionalString(PropertySource);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            PropertyName = reader.ReadOptionalString();
            PropertyValue = reader.ReadOptionalString();
            PropertySource = reader.ReadOptionalString();
        }
    }
}
