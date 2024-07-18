// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the environment variable read event.
    /// </summary>
    public class EnvironmentVariableReadEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes an instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
        public EnvironmentVariableReadEventArgs()
        {
        }

        /// <summary>
        /// Initializes an instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
        /// <param name="environmentVariableName">The name of the environment variable that was read.</param>
        /// <param name="message">The value of the environment variable that was read.</param>
        /// <param name="helpKeyword">Help keyword.</param>
        /// <param name="senderName">The name of the sender of the event.</param>
        /// <param name="importance">The importance of the message.</param>
        public EnvironmentVariableReadEventArgs(
            string environmentVariableName,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low)
            : base(message, helpKeyword, senderName, importance) => EnvironmentVariableName = environmentVariableName;

        /// <summary>
        /// Initializes an instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
        /// <param name="environmentVarName">The name of the environment variable that was read.</param>
        /// <param name="environmentVarValue">The value of the environment variable that was read.</param>
        /// <param name="file">file associated with the event</param>
        /// <param name="line">line number (0 if not applicable)</param>
        /// <param name="column">column number (0 if not applicable)</param>
        public EnvironmentVariableReadEventArgs(
            string environmentVarName,
            string environmentVarValue,
            string file,
            int line,
            int column)
            : base(environmentVarValue, file, line, column, MessageImportance.Low) => EnvironmentVariableName = environmentVarName;

        /// <summary>
        /// The name of the environment variable that was read.
        /// </summary>
        public string EnvironmentVariableName { get; set; }

        // <summary>
        // The file name where environment variable is used.
        // </summary>
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(EnvironmentVariableName);
            writer.Write7BitEncodedInt(LineNumber);
            writer.Write7BitEncodedInt(ColumnNumber);
            writer.WriteOptionalString(File);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            EnvironmentVariableName = reader.ReadString();
            LineNumber = reader.Read7BitEncodedInt();
            ColumnNumber = reader.Read7BitEncodedInt();
            File = reader.ReadOptionalString() ?? string.Empty;
        }
    }
}
