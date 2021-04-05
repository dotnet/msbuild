// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for telemetry events.
    /// </summary>
    [Serializable]
    public sealed class TelemetryEventArgs : BuildEventArgs
    {
        /// <summary>
        /// Gets or sets the name of the event.
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// Gets or sets a list of properties associated with the event.
        /// </summary>
        public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(EventName);
            int count = Properties?.Count ?? 0;
            writer.Write7BitEncodedInt(count);

            foreach (var kvp in Properties)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            EventName = reader.ReadOptionalString();
            int count = reader.Read7BitEncodedInt();

            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                string value = reader.ReadString();
                Properties.Add(key, value);
            }
        }
    }
}
