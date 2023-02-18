// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for error events with additional content
    /// </summary>
    // WARNING: marking a type [Serializable] without implementing
    // ISerializable imposes a serialization contract -- it is a
    // promise to never change the type's fields i.e. the type is
    // immutable; adding new fields in the next version of the type
    // without following certain special FX guidelines, can break both
    // forward and backward compatibility
    [Serializable]
    public class BuildErrorWithContentEventArgs : BuildErrorEventArgs
    {
        public string AdditionalContentType { get; protected set; }
        public string AdditionalContentText { get; protected set; }
        public string AdditionalContentSimpleText { get; protected set; }

        public BuildErrorWithContentEventArgs(
            string subcategory,
            string code,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            string helpKeyword,
            string senderName,
            string helpLink,
            DateTime eventTimestamp,
            string additionalContentType,
            string additionalContentText,
            string additionalContentSimpleText,
            params object[] messageArgs)
            : base(
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName,
                helpLink,
                eventTimestamp,
                messageArgs)
        {
            AdditionalContentType = additionalContentType;
            AdditionalContentText = additionalContentText;
            AdditionalContentSimpleText = additionalContentSimpleText;
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            AdditionalContentType = reader.ReadByte() == 0 ? null : reader.ReadString();
            AdditionalContentText = reader.ReadByte() == 0 ? null : reader.ReadString();
            AdditionalContentSimpleText = reader.ReadByte() == 0 ? null : reader.ReadString();
        }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.WriteOptionalString(AdditionalContentType);
            writer.WriteOptionalString(AdditionalContentText);
            writer.WriteOptionalString(AdditionalContentSimpleText);
        }
    }
}
