// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the environment variable read event.
    /// </summary>
    public sealed class ExtendedEnvironmentVariableReadEventArgs : BuildMessageEventArgs
    {
        public ExtendedEnvironmentVariableReadEventArgs() { }

        /// <summary>
        /// Initializes an instance of the ExtendedEnvironmentVariableReadEventArgs class.
        /// </summary>
        /// <param name="environmentVarName">The name of the environment variable that was read.</param>
        /// <param name="environmentVarValue">The value of the environment variable that was read.</param>
        /// <param name="file">file associated with the event</param>
        /// <param name="line">line number (0 if not applicable)</param>
        /// <param name="column">column number (0 if not applicable)</param>
        /// <param name="helpKeyword">Help keyword.</param>
        /// <param name="senderName">The name of the sender of the event.</param>
        public ExtendedEnvironmentVariableReadEventArgs(
            string environmentVarName,
            string environmentVarValue,
            string file,
            int line,
            int column,
            string? helpKeyword = null,
            string? senderName = null)
            : base(environmentVarValue, helpKeyword, senderName, MessageImportance.Normal)
        {
            EnvironmentVariableName = environmentVarName;
            FileName = file;
            LineNumber = line;
            ColumnNumber = column;
        }

        /// <summary>
        /// The file name where environment variable is used.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The environment variable name.
        /// </summary>
        public string EnvironmentVariableName { get; set; } = string.Empty;

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(EnvironmentVariableName);
            writer.Write7BitEncodedInt(LineNumber);
            writer.Write7BitEncodedInt(ColumnNumber);
            writer.WriteOptionalString(FileName);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            EnvironmentVariableName = reader.ReadString();
            LineNumber = reader.Read7BitEncodedInt();
            ColumnNumber = reader.Read7BitEncodedInt();
            FileName = reader.ReadOptionalString() ?? string.Empty;
        }
    }
}
