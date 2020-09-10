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

        BuildWarningEventArgs IMessagePackFormatter<BuildWarningEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            int length = reader.ReadArrayHeader();
            string message = null;
            string helpKeyword = null;
            string senderName = null;
            int columnNumber = default;
            int endColumnNumber = default;
            int endLineNumber = default;
            int lineNumber = default;
            string code = default;
            string file = default;
            string subCategory = default;

            for (int key = 0; key < length; key++)
            {
                switch (key)
                {
                    case 0:
                        message = reader.ReadString();
                        break;
                    case 1:
                        helpKeyword = reader.ReadString();
                        break;
                    case 2:
                        senderName = reader.ReadString();
                        break;
                    case 3:
                        columnNumber = reader.ReadInt32();
                        break;
                    case 4:
                        endColumnNumber = reader.ReadInt32();
                        break;
                    case 5:
                        endLineNumber = reader.ReadInt32();
                        break;
                    case 6:
                        lineNumber = reader.ReadInt32();
                        break;
                    case 7:
                        code = reader.ReadString();
                        break;
                    case 8:
                        file = reader.ReadString();
                        break;
                    case 9:
                        subCategory = reader.ReadString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }


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

            reader.Depth--;

            return buildEvent;
        }

        void IMessagePackFormatter<BuildWarningEventArgs>.Serialize(ref MessagePackWriter writer, BuildWarningEventArgs value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(10);
            writer.Write(value.Message);
            writer.Write(value.HelpKeyword);
            writer.Write(value.SenderName);
            writer.Write(value.ColumnNumber);
            writer.Write(value.EndColumnNumber);
            writer.Write(value.EndLineNumber);
            writer.Write(value.LineNumber);
            writer.Write(value.Code);
            writer.Write(value.File);
            writer.Write(value.Subcategory);
        }

        BuildErrorEventArgs IMessagePackFormatter<BuildErrorEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            int length = reader.ReadArrayHeader();
            string message = null;
            string helpKeyword = null;
            string senderName = null;
            int columnNumber = default;
            int endColumnNumber = default;
            int endLineNumber = default;
            int lineNumber = default;
            string code = default;
            string file = default;
            string subCategory = default;

            for (int key = 0; key < length; key++)
            {
                switch (key)
                {
                    case 0:
                        message = reader.ReadString();
                        break;
                    case 1:
                        helpKeyword = reader.ReadString();
                        break;
                    case 2:
                        senderName = reader.ReadString();
                        break;
                    case 3:
                        columnNumber = reader.ReadInt32();
                        break;
                    case 4:
                        endColumnNumber = reader.ReadInt32();
                        break;
                    case 5:
                        endLineNumber = reader.ReadInt32();
                        break;
                    case 6:
                        lineNumber = reader.ReadInt32();
                        break;
                    case 7:
                        code = reader.ReadString();
                        break;
                    case 8:
                        file = reader.ReadString();
                        break;
                    case 9:
                        subCategory = reader.ReadString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

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
            reader.Depth--;

            return buildEvent;
        }


        void IMessagePackFormatter<BuildErrorEventArgs>.Serialize(ref MessagePackWriter writer, BuildErrorEventArgs value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(10);
            writer.Write(value.Message);
            writer.Write(value.HelpKeyword);
            writer.Write(value.SenderName);
            writer.Write(value.ColumnNumber);
            writer.Write(value.EndColumnNumber);
            writer.Write(value.EndLineNumber);
            writer.Write(value.LineNumber);
            writer.Write(value.Code);
            writer.Write(value.File);
            writer.Write(value.Subcategory);
        }

        BuildMessageEventArgs IMessagePackFormatter<BuildMessageEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            int length = reader.ReadArrayHeader();
            string message = null;
            string helpKeyword = null;
            string senderName = null;
            int columnNumber = default;
            int endColumnNumber = default;
            int endLineNumber = default;
            int lineNumber = default;
            string code = default;
            string file = default;
            string subCategory = default;
            int importance = default;

            for (int key = 0; key < length; key++)
            {
                switch (key)
                {
                    case 0:
                        message = reader.ReadString();
                        break;
                    case 1:
                        helpKeyword = reader.ReadString();
                        break;
                    case 2:
                        senderName = reader.ReadString();
                        break;
                    case 3:
                        columnNumber = reader.ReadInt32();
                        break;
                    case 4:
                        endColumnNumber = reader.ReadInt32();
                        break;
                    case 5:
                        endLineNumber = reader.ReadInt32();
                        break;
                    case 6:
                        lineNumber = reader.ReadInt32();
                        break;
                    case 7:
                        code = reader.ReadString();
                        break;
                    case 8:
                        file = reader.ReadString();
                        break;
                    case 9:
                        subCategory = reader.ReadString();
                        break;
                    case 10:
                        importance = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            BuildMessageEventArgs buildEvent =
               new BuildMessageEventArgs(
                       subCategory,
                       code,
                       file,
                       lineNumber,
                       columnNumber,
                       endLineNumber,
                       endColumnNumber,
                       message,
                       helpKeyword,
                       senderName,
                       (MessageImportance)importance);
            reader.Depth--;

            return buildEvent;
        }

        void IMessagePackFormatter<BuildMessageEventArgs>.Serialize(ref MessagePackWriter writer, BuildMessageEventArgs value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            int importance = (int)value.Importance;

            writer.WriteArrayHeader(11);
            writer.Write(value.Message);
            writer.Write(value.HelpKeyword);
            writer.Write(value.SenderName);
            writer.Write(value.ColumnNumber);
            writer.Write(value.EndColumnNumber);
            writer.Write(value.EndLineNumber);
            writer.Write(value.LineNumber);
            writer.Write(value.Code);
            writer.Write(value.File);
            writer.Write(value.Subcategory);
            writer.Write(importance);
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
            if (value == null)
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
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            int length = reader.ReadArrayHeader();
            string message = null;
            string helpKeyword = null;
            string senderName = null;
            string projectFile = default;
            string targetNames = default;

            for (int key = 0; key < length; key++)
            {
                switch (key)
                {
                    case 0:
                        message = reader.ReadString();
                        break;
                    case 1:
                        helpKeyword = reader.ReadString();
                        break;
                    case 2:
                        senderName = reader.ReadString();
                        break;
                    case 3:
                        projectFile = reader.ReadString();
                        break;
                    case 4:
                        targetNames = reader.ReadString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            ExternalProjectStartedEventArgs buildEvent =
               new ExternalProjectStartedEventArgs(
                       message,
                       helpKeyword,
                       senderName,
                       projectFile,
                       targetNames);
            reader.Depth--;

            return buildEvent;
        }

        void IMessagePackFormatter<ExternalProjectStartedEventArgs>.Serialize(ref MessagePackWriter writer, ExternalProjectStartedEventArgs value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(5);
            writer.Write(value.Message);
            writer.Write(value.HelpKeyword);
            writer.Write(value.SenderName);
            writer.Write(value.ProjectFile);
            writer.Write(value.TargetNames);
        }

        ExternalProjectFinishedEventArgs IMessagePackFormatter<ExternalProjectFinishedEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            int length = reader.ReadArrayHeader();
            string message = null;
            string helpKeyword = null;
            string senderName = null;
            string projectFile = default;
            bool succeeded = default;

            for (int key = 0; key < length; key++)
            {
                switch (key)
                {
                    case 0:
                        message = reader.ReadString();
                        break;
                    case 1:
                        helpKeyword = reader.ReadString();
                        break;
                    case 2:
                        senderName = reader.ReadString();
                        break;
                    case 3:
                        projectFile = reader.ReadString();
                        break;
                    case 4:
                        succeeded = reader.ReadBoolean();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            ExternalProjectFinishedEventArgs buildEvent =
               new ExternalProjectFinishedEventArgs(
                       message,
                       helpKeyword,
                       senderName,
                       projectFile,
                       succeeded);
            reader.Depth--;

            return buildEvent;
        }

        void IMessagePackFormatter<ExternalProjectFinishedEventArgs>.Serialize(ref MessagePackWriter writer, ExternalProjectFinishedEventArgs value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(5);
            writer.Write(value.Message);
            writer.Write(value.HelpKeyword);
            writer.Write(value.SenderName);
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
