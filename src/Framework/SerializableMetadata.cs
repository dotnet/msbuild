// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Serializable wrapper for immutable metadata dictionaries.
    /// Safe to use across AppDomains.
    /// </summary>
    [Serializable]
    internal readonly struct SerializableMetadata : ISerializable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializableMetadata"/> struct.
        /// </summary>
        /// <param name="dictionary">The metadata dictionary to set.</param>
        /// <remarks>
        /// Calling this constructor implies that the instance value is usable. As such, a null input will be converted
        /// to an empty instance.
        /// </remarks>
        public SerializableMetadata(ImmutableDictionary<string, string> dictionary) =>
            Dictionary = dictionary ?? ImmutableDictionaryExtensions.EmptyMetadata;

        public SerializableMetadata(SerializationInfo info, StreamingContext context)
        {
            bool hasValue = info.GetBoolean("hasValue");
            if (hasValue)
            {
                object entries = info.GetValue("value", typeof(KeyValuePair<string, string>[]))!;
                Dictionary = ImmutableDictionaryExtensions.EmptyMetadata.AddRange((KeyValuePair<string, string>[])entries);
            }
        }

        /// <summary>
        /// Gets the backing metadata dictionary.
        /// </summary>
        internal ImmutableDictionary<string, string>? Dictionary { get; }

        /// <summary>
        /// Gets a value indicating whether the wrapped dictionary represents a usable instance.
        /// </summary>
        /// <remarks>
        /// Since SerializableMetadata is a struct, this allows the default value to represent an unusable instance.
        /// </remarks>
        internal bool HasValue => Dictionary != null;

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("hasValue", HasValue);
            if (HasValue)
            {
                info.AddValue("value", Dictionary!.ToArray());
            }
        }
    }
}
