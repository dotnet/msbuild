// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal sealed class BuildEventArgsFormatter
        : IMessagePackFormatter<BuildEventArgs>, IMessagePackFormatter<BuildErrorEventArgs>, IMessagePackFormatter<BuildWarningEventArgs>, IMessagePackFormatter<BuildMessageEventArgs>,
         IMessagePackFormatter<CustomBuildEventArgs>, IMessagePackFormatter<ExternalProjectStartedEventArgs>, IMessagePackFormatter<ExternalProjectFinishedEventArgs>
    {

        internal static readonly IMessagePackFormatter Instance = new BuildEventArgsFormatter();

        private BuildEventArgsFormatter() { }

        #region BuildWarningEventArgs
        BuildWarningEventArgs IMessagePackFormatter<BuildWarningEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            _ = reader.ReadArrayHeader();
            string message = reader.ReadString();
            string helpKeyword = reader.ReadString();
            string senderName = reader.ReadString();
            int columnNumber = reader.ReadInt32();
            int endColumnNumber = reader.ReadInt32();
            int endLineNumber = reader.ReadInt32();
            int lineNumber = reader.ReadInt32();
            string code = reader.ReadString();
            string file = reader.ReadString();
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
        #endregion

        #region BuildErrorEventArgs
        BuildErrorEventArgs IMessagePackFormatter<BuildErrorEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            _ = reader.ReadArrayHeader();
            string message = reader.ReadString();
            string helpKeyword = reader.ReadString();
            string senderName = reader.ReadString();
            int columnNumber = reader.ReadInt32();
            int endColumnNumber = reader.ReadInt32();
            int endLineNumber = reader.ReadInt32();
            int lineNumber = reader.ReadInt32();
            string code = reader.ReadString();
            string file = reader.ReadString();
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
        #endregion

        #region BuildMessageEventArgs
        BuildMessageEventArgs IMessagePackFormatter<BuildMessageEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            _ = reader.ReadArrayHeader();
            string message = reader.ReadString();
            string helpKeyword = reader.ReadString();
            string senderName = reader.ReadString();
            int columnNumber = reader.ReadInt32();
            int endColumnNumber = reader.ReadInt32();
            int endLineNumber = reader.ReadInt32();
            int lineNumber = reader.ReadInt32();
            string code = reader.ReadString();
            string file = reader.ReadString();
            string subCategory = reader.ReadString();
            int importance = reader.ReadInt32();

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
        #endregion

        #region CustomBuildEventArgs
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
            _ = reader.ReadArrayHeader();
            string message = reader.ReadString();
            string helpKeyword = reader.ReadString();
            string senderName = reader.ReadString();
            string projectFile = reader.ReadString();
            string targetNames = reader.ReadString();

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
            _ = reader.ReadArrayHeader();
            string message = reader.ReadString();
            string helpKeyword = reader.ReadString();
            string senderName = reader.ReadString();
            string projectFile = reader.ReadString();
            bool succeeded = reader.ReadBoolean();

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
        #endregion

        BuildEventArgs IMessagePackFormatter<BuildEventArgs>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            int _ = reader.ReadArrayHeader();
            int customType = reader.ReadInt32();
            BuildEventArgs buildEvent = customType switch
            {
                1 => (this as IMessagePackFormatter<BuildErrorEventArgs>).Deserialize(ref reader, options),
                2 => (this as IMessagePackFormatter<BuildWarningEventArgs>).Deserialize(ref reader, options),
                3 => (this as IMessagePackFormatter<BuildMessageEventArgs>).Deserialize(ref reader, options),
                4 => (this as IMessagePackFormatter<CustomBuildEventArgs>).Deserialize(ref reader, options),
                _ => null
            };
            reader.Depth--;

            ErrorUtilities.VerifyThrowInternalNull(buildEvent, nameof(buildEvent));

            return buildEvent;
        }

        void IMessagePackFormatter<BuildEventArgs>.Serialize(ref MessagePackWriter writer, BuildEventArgs value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            int customType = value switch
            {
                BuildErrorEventArgs _ => 1,
                BuildWarningEventArgs _ => 2,
                BuildMessageEventArgs _ => 3,
                CustomBuildEventArgs _ => 4,
                _ => 0
            };

            writer.WriteArrayHeader(2);
            writer.WriteInt32(customType);
            switch (customType)
            {
                case 1:
                    (this as IMessagePackFormatter<BuildErrorEventArgs>).Serialize(ref writer, value as BuildErrorEventArgs, options);
                    break;
                case 2:
                    (this as IMessagePackFormatter<BuildWarningEventArgs>).Serialize(ref writer, value as BuildWarningEventArgs, options);
                    break;
                case 3:
                    (this as IMessagePackFormatter<BuildMessageEventArgs>).Serialize(ref writer, value as BuildMessageEventArgs, options);
                    break;
                case 4:
                    (this as IMessagePackFormatter<CustomBuildEventArgs>).Serialize(ref writer, value as CustomBuildEventArgs, options);
                    break;
                default:
                    ErrorUtilities.ThrowInternalError("Unexpected formatter id");
                    break;
            }
        }
    }
}
