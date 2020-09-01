// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Nerdbank.Streams;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal static partial class BuildEventArgsFormatter
    {
        public static IMessagePackFormatter<BuildErrorEventArgs> ErrorFormatter { get; } = new BuildError();
        public static IMessagePackFormatter<BuildWarningEventArgs> WarningFormatter { get; } = new BuildWarning();
        public static IMessagePackFormatter<BuildMessageEventArgs> MessageFormatter { get; } = new BuildMessage();
        public static IMessagePackFormatter<CustomBuildEventArgs> CustomFormatter { get; } = new Custom();


        private abstract class Formatter<TArg> : IMessagePackFormatter<TArg> where TArg : BuildEventArgs
        {
            public TArg Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                ReadOnlySequence<byte>? buffer = reader.ReadBytes();

                if (!buffer.HasValue)
                {
                    return null;
                }

                try
                {
                    BinaryReader binaryReader = new BinaryReader(buffer.Value.AsStream());
                    TArg arg = GetEventArgInstance();
                    // We are communicating with current MSBuild RAR node, if not something is really wrong
                    arg.CreateFromStream(binaryReader, int.MaxValue);
                    return arg;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            public void Serialize(ref MessagePackWriter writer, TArg value, MessagePackSerializerOptions options)
            {
                if (value is null)
                {
                    writer.Write((byte[])null);
                    return;
                }

                using MemoryStream stream = new MemoryStream();
                using BinaryWriter binaryWriter = new BinaryWriter(stream);

                value.WriteToStream(binaryWriter);
                writer.Write(stream.ToArray());
            }

            protected abstract TArg GetEventArgInstance();
        }

        private sealed class BuildError : Formatter<BuildErrorEventArgs>, IMessagePackFormatter<BuildErrorEventArgs>
        {
            protected override BuildErrorEventArgs GetEventArgInstance() => new BuildErrorEventArgs();
        }

        private sealed class BuildMessage : Formatter<BuildMessageEventArgs>, IMessagePackFormatter<BuildMessageEventArgs>
        {
            protected override BuildMessageEventArgs GetEventArgInstance() => new BuildMessageEventArgs();
        }

        private sealed class BuildWarning : Formatter<BuildWarningEventArgs>, IMessagePackFormatter<BuildWarningEventArgs>
        {
            protected override BuildWarningEventArgs GetEventArgInstance() => new BuildWarningEventArgs();
        }

        private sealed class Custom : IMessagePackFormatter<CustomBuildEventArgs>
        {
            private static IMessagePackFormatter<ExternalProjectFinishedEventArgs> ExternalProjectFinishedFormatter = new ExternalProjectFinished();
            private static IMessagePackFormatter<ExternalProjectStartedEventArgs> ExternalProjectStartedFormatter = new ExternalProjectStarted();

            public CustomBuildEventArgs Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                ushort formatter = reader.ReadUInt16();

                switch (formatter)
                {
                    case 1:
                        return ExternalProjectStartedFormatter.Deserialize(ref reader, options);
                    case 2:
                        return ExternalProjectFinishedFormatter.Deserialize(ref reader, options);
                    default:
                        ErrorUtilities.ThrowInternalError("Unexpected formatter id");
                        return null; // Never hits...
                }
            }

            public void Serialize(ref MessagePackWriter writer, CustomBuildEventArgs value, MessagePackSerializerOptions options)
            {
                ushort formatterId = value switch
                {
                    ExternalProjectStartedEventArgs _ => 1,
                    ExternalProjectFinishedEventArgs _ => 2,
                    _ => 0
                };

                if (formatterId == 0)
                {
                    ErrorUtilities.ThrowArgumentOutOfRange(nameof(value));
                }

                writer.WriteUInt16(formatterId);

                switch (formatterId)
                {
                    case 1:
                        ExternalProjectStartedFormatter.Serialize(ref writer, value as ExternalProjectStartedEventArgs, options);
                        break;
                    case 2:
                        ExternalProjectFinishedFormatter.Serialize(ref writer, value as ExternalProjectFinishedEventArgs, options);
                        break;
                    default:
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                        break;
                }
            }

            private class ExternalProjectFinished : Formatter<ExternalProjectFinishedEventArgs>, IMessagePackFormatter<ExternalProjectFinishedEventArgs>
            {
                protected override ExternalProjectFinishedEventArgs GetEventArgInstance() => new ExternalProjectFinishedEventArgs();
            }

            private class ExternalProjectStarted : Formatter<ExternalProjectStartedEventArgs>, IMessagePackFormatter<ExternalProjectStartedEventArgs>
            {
                protected override ExternalProjectStartedEventArgs GetEventArgInstance() => new ExternalProjectStartedEventArgs();
            }
        }
    }
}
