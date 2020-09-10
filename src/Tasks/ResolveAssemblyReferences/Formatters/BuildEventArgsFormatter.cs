// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal sealed class BuildEventArgsFormatter
        : IMessagePackFormatter<BuildErrorEventArgs>, IMessagePackFormatter<BuildWarningEventArgs>, IMessagePackFormatter<BuildMessageEventArgs>,
         IMessagePackFormatter<CustomBuildEventArgs>, IMessagePackFormatter<ExternalProjectStartedEventArgs>, IMessagePackFormatter<ExternalProjectFinishedEventArgs>
    {

        internal static readonly IMessagePackFormatter Instance = new BuildEventArgsFormatter();

        private BuildEventArgsFormatter() { }

        private bool DeserializeBase(ref MessagePackReader reader, out string message, out string helpKeyword, out string senderName)
        {
            message = null;
            helpKeyword = null;
            senderName = null;

            if (reader.TryReadNil())
            {
                return true;
            }

            message = reader.ReadString();
            helpKeyword = reader.ReadString();
            senderName = reader.ReadString();

            return false;
        }

        private bool SerializeBase(ref MessagePackWriter wrtier, BuildEventArgs buildEvent)
        {
            if (buildEvent == null)
            {
                wrtier.WriteNil();
                return true;
            }

            wrtier.Write(buildEvent.Message);
            wrtier.Write(buildEvent.HelpKeyword);
            wrtier.Write(buildEvent.SenderName);

            return false;
        }


        BuildWarningEventArgs IMessagePackFormatter<BuildWarningEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (DeserializeBase(ref reader, out string message, out string helpKeyword, out string senderName))
            {
                return null;
            }

            string code = reader.ReadString();
            int columnNumber = reader.ReadInt32();
            int endColumnNumber = reader.ReadInt32();
            int endLineNumber = reader.ReadInt32();
            string file = reader.ReadString();
            int lineNumber = reader.ReadInt32();
            string subCategory = reader.ReadString();

            BuildWarningEventArgs buildEvent =
                new BuildWarningEventArgs(
                        subCategory,
                        code,
                        file,
                        lineNumber,
                        columnNumber,
                        endLineNumber,
                        endColumnNumber,
                        message,
                        helpKeyword,
                        senderName);

            return buildEvent;
        }

        void IMessagePackFormatter<BuildWarningEventArgs>.Serialize(ref MessagePackWriter writer, BuildWarningEventArgs value, MessagePackSerializerOptions options)
        {
            if (SerializeBase(ref writer, value))
            {
                return;
            }

            writer.Write(value.Code);
            writer.WriteInt32(value.ColumnNumber);
            writer.WriteInt32(value.EndColumnNumber);
            writer.WriteInt32(value.EndLineNumber);
            writer.Write(value.File);
            writer.WriteInt32(value.LineNumber);
            writer.Write(value.Subcategory);
        }

        BuildErrorEventArgs IMessagePackFormatter<BuildErrorEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (DeserializeBase(ref reader, out string message, out string helpKeyword, out string senderName))
            {
                return null;
            }

            string code = reader.ReadString();
            int columnNumber = reader.ReadInt32();
            int endColumnNumber = reader.ReadInt32();
            int endLineNumber = reader.ReadInt32();
            string file = reader.ReadString();
            int lineNumber = reader.ReadInt32();
            string subCategory = reader.ReadString();

            BuildErrorEventArgs buildEvent =
                new BuildErrorEventArgs(
                        subCategory,
                        code,
                        file,
                        lineNumber,
                        columnNumber,
                        endLineNumber,
                        endColumnNumber,
                        message,
                        helpKeyword,
                        senderName);

            return buildEvent;
        }


        void IMessagePackFormatter<BuildErrorEventArgs>.Serialize(ref MessagePackWriter writer, BuildErrorEventArgs value, MessagePackSerializerOptions options)
        {
            if (SerializeBase(ref writer, value))
            {
                return;
            }

            writer.Write(value.Code);
            writer.WriteInt32(value.ColumnNumber);
            writer.WriteInt32(value.EndColumnNumber);
            writer.WriteInt32(value.EndLineNumber);
            writer.Write(value.File);
            writer.WriteInt32(value.LineNumber);
            writer.Write(value.Subcategory);
        }

        BuildMessageEventArgs IMessagePackFormatter<BuildMessageEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (DeserializeBase(ref reader, out string message, out string helpKeyword, out string senderName))
            {
                return null;
            }

            int importance = reader.ReadInt32();

            return new BuildMessageEventArgs(message, helpKeyword, senderName, (MessageImportance)importance);
        }

        void IMessagePackFormatter<BuildMessageEventArgs>.Serialize(ref MessagePackWriter writer, BuildMessageEventArgs value, MessagePackSerializerOptions options)
        {
            if (SerializeBase(ref writer, value))
            {
                return;
            }

            int importance = (int)value.Importance;

            writer.WriteInt32(importance);
        }

        CustomBuildEventArgs IMessagePackFormatter<CustomBuildEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            int customType = reader.ReadInt32();

            switch (customType)
            {
                case 1:
                    return (this as IMessagePackFormatter<ExternalProjectStartedEventArgs>).Deserialize(ref reader, options);
                case 2:
                    return (this as IMessagePackFormatter<ExternalProjectFinishedEventArgs>).Deserialize(ref reader, options);
                default:
                    ErrorUtilities.ThrowInternalError("Unexpected formatter id");
                    return null;
            }
        }

        void IMessagePackFormatter<CustomBuildEventArgs>.Serialize(ref MessagePackWriter writer, CustomBuildEventArgs value, MessagePackSerializerOptions options)
        {
            if(value == null)
            {
                writer.WriteNil();
                return;
            }

            int customType = value switch
            {
                ExternalProjectStartedEventArgs _ => 1,
                ExternalProjectFinishedEventArgs _ => 2,
                _ => 0
            };

            writer.WriteInt32(customType);

            switch (customType)
            {
                case 1:
                    (this as IMessagePackFormatter<ExternalProjectStartedEventArgs>).Serialize(ref writer, value as ExternalProjectStartedEventArgs, options);
                    break;
                case 2:
                    (this as IMessagePackFormatter<ExternalProjectFinishedEventArgs>).Serialize(ref writer, value as ExternalProjectFinishedEventArgs, options);
                    break;
                default:
                    ErrorUtilities.ThrowInternalError("Unexpected formatter id");
                    break;
            }
        }

        ExternalProjectStartedEventArgs IMessagePackFormatter<ExternalProjectStartedEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (DeserializeBase(ref reader, out string message, out string helpKeyword, out string senderName))
            {
                return null;
            }

            string projectFile = reader.ReadString();
            string targetNames = reader.ReadString();

            return new ExternalProjectStartedEventArgs(
                   message,
                   helpKeyword,
                   senderName,
                   projectFile,
                   targetNames);
        }

        void IMessagePackFormatter<ExternalProjectStartedEventArgs>.Serialize(ref MessagePackWriter writer, ExternalProjectStartedEventArgs value, MessagePackSerializerOptions options)
        {
            if (SerializeBase(ref writer, value))
            {
                return;
            }

            writer.Write(value.ProjectFile);
            writer.Write(value.TargetNames);
        }

        ExternalProjectFinishedEventArgs IMessagePackFormatter<ExternalProjectFinishedEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (DeserializeBase(ref reader, out string message, out string helpKeyword, out string senderName))
            {
                return null;
            }

            string projectFile = reader.ReadString();
            bool succeeded = reader.ReadBoolean();

            return new ExternalProjectFinishedEventArgs(
                    message,
                    helpKeyword,
                    senderName,
                    projectFile,
                    succeeded);
        }

        void IMessagePackFormatter<ExternalProjectFinishedEventArgs>.Serialize(ref MessagePackWriter writer, ExternalProjectFinishedEventArgs value, MessagePackSerializerOptions options)
        {
            if (SerializeBase(ref writer, value))
            {
                return;
            }

            writer.Write(value.ProjectFile);
            writer.Write(value.Succeeded);
        }


        //private abstract class Formatter<TArg> : IMessagePackFormatter<TArg> where TArg : BuildEventArgs
        //{
        //    public TArg Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        //    {
        //        ReadOnlySequence<byte>? buffer = reader.ReadBytes();

        //        if (!buffer.HasValue)
        //        {
        //            return null;
        //        }

        //        try
        //        {
        //            BinaryReader binaryReader = new BinaryReader(buffer.Value.AsStream());
        //            TArg arg = GetEventArgInstance();
        //            // We are communicating with current MSBuild RAR node, if not something is really wrong
        //            arg.CreateFromStream(binaryReader, int.MaxValue);
        //            return arg;
        //        }
        //        catch (Exception)
        //        {
        //            return null;
        //        }
        //    }

        //    public void Serialize(ref MessagePackWriter writer, TArg value, MessagePackSerializerOptions options)
        //    {
        //        if (value is null)
        //        {
        //            writer.Write((byte[])null);
        //            return;
        //        }

        //        using MemoryStream stream = new MemoryStream();
        //        using BinaryWriter binaryWriter = new BinaryWriter(stream);

        //        value.WriteToStream(binaryWriter);
        //        writer.Write(stream.ToArray());
        //    }

        //    protected abstract TArg GetEventArgInstance();
        //}

        //private sealed class BuildError : Formatter<BuildErrorEventArgs>, IMessagePackFormatter<BuildErrorEventArgs>
        //{
        //    protected override BuildErrorEventArgs GetEventArgInstance() => new BuildErrorEventArgs();
        //}

        //private sealed class BuildMessage : Formatter<BuildMessageEventArgs>, IMessagePackFormatter<BuildMessageEventArgs>
        //{
        //    protected override BuildMessageEventArgs GetEventArgInstance() => new BuildMessageEventArgs();
        //}

        //private sealed class BuildWarning : Formatter<BuildWarningEventArgs>, IMessagePackFormatter<BuildWarningEventArgs>
        //{
        //    protected override BuildWarningEventArgs GetEventArgInstance() => new BuildWarningEventArgs();
        //}

        //private sealed class Custom : IMessagePackFormatter<CustomBuildEventArgs>
        //{
        //    private static IMessagePackFormatter<ExternalProjectFinishedEventArgs> ExternalProjectFinishedFormatter = new ExternalProjectFinished();
        //    private static IMessagePackFormatter<ExternalProjectStartedEventArgs> ExternalProjectStartedFormatter = new ExternalProjectStarted();

        //    public CustomBuildEventArgs Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        //    {
        //        ushort formatter = reader.ReadUInt16();

        //        switch (formatter)
        //        {
        //            case 1:
        //                return ExternalProjectStartedFormatter.Deserialize(ref reader, options);
        //            case 2:
        //                return ExternalProjectFinishedFormatter.Deserialize(ref reader, options);
        //            default:
        //                ErrorUtilities.ThrowInternalError("Unexpected formatter id");
        //                return null; // Never hits...
        //        }
        //    }

        //    public void Serialize(ref MessagePackWriter writer, CustomBuildEventArgs value, MessagePackSerializerOptions options)
        //    {
        //        ushort formatterId = value switch
        //        {
        //            ExternalProjectStartedEventArgs _ => 1,
        //            ExternalProjectFinishedEventArgs _ => 2,
        //            _ => 0
        //        };

        //        if (formatterId == 0)
        //        {
        //            ErrorUtilities.ThrowArgumentOutOfRange(nameof(value));
        //        }

        //        writer.WriteUInt16(formatterId);

        //        switch (formatterId)
        //        {
        //            case 1:
        //                ExternalProjectStartedFormatter.Serialize(ref writer, value as ExternalProjectStartedEventArgs, options);
        //                break;
        //            case 2:
        //                ExternalProjectFinishedFormatter.Serialize(ref writer, value as ExternalProjectFinishedEventArgs, options);
        //                break;
        //            default:
        //                ErrorUtilities.ThrowInternalErrorUnreachable();
        //                break;
        //        }
        //    }

        //    private class ExternalProjectFinished : Formatter<ExternalProjectFinishedEventArgs>, IMessagePackFormatter<ExternalProjectFinishedEventArgs>
        //    {
        //        protected override ExternalProjectFinishedEventArgs GetEventArgInstance() => new ExternalProjectFinishedEventArgs();
        //    }

        //    private class ExternalProjectStarted : Formatter<ExternalProjectStartedEventArgs>, IMessagePackFormatter<ExternalProjectStartedEventArgs>
        //    {
        //        protected override ExternalProjectStartedEventArgs GetEventArgInstance() => new ExternalProjectStartedEventArgs();
        //    }
        //}
    }
}
