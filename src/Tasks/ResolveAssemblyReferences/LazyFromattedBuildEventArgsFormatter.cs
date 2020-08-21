// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Nerdbank.Streams;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal sealed class LazyFromattedBuildEventArgsFormatter : IMessagePackFormatter<LazyFormattedBuildEventArgs>
    {
        internal static readonly IMessagePackFormatter Instance = new LazyFromattedBuildEventArgsFormatter();

        private LazyFromattedBuildEventArgsFormatter() { }

        public LazyFormattedBuildEventArgs Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            ReadOnlySequence<byte>? buffer = reader.ReadBytes();

            if (!buffer.HasValue)
                return null;
            try
            {
                // TODO: Remove this!
                IFormatter formatter = new BinaryFormatter();
                return (LazyFormattedBuildEventArgs)formatter.Deserialize(buffer.Value.AsStream());
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Serialize(ref MessagePackWriter writer, LazyFormattedBuildEventArgs value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.Write((byte[])null);
                return;
            }

            using MemoryStream stream = new MemoryStream();

            // TODO: Remove this!
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, value);
            writer.Write(stream.ToArray());
        }
    }
}
