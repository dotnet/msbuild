// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the environment variable read event.
    /// </summary>
    public sealed class ExtendedEnvironmentVariableReadEventArgs : CustomBuildEventArgs, IExtendedBuildEventArgs
    {
        /// <summary>
        /// Default constructor. Used for deserialization.
        /// </summary>
        internal ExtendedEnvironmentVariableReadEventArgs() { }

        /// <inheritdoc />
        public string ExtendedType { get; set; } = string.Empty;

        /// <inheritdoc />
        public Dictionary<string, string?>? ExtendedMetadata { get; set; }

        /// <inheritdoc />
        public string? ExtendedData { get; set; }

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
            : base(environmentVarValue, helpKeyword, senderName)
        {
            EnvironmentVariableName = environmentVarName;
            File = file;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// The name of the environment variable that was read.
        /// </summary>
        public string EnvironmentVariableName { get; set; } = string.Empty;

        /// <summary>
        /// The line number where environment variable is used.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// The column where environment variable is used.
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// The file name where environment variable is used.
        /// </summary>
        public string File { get; set; } = string.Empty;

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(EnvironmentVariableName);
            writer.Write7BitEncodedInt(Line);
            writer.Write7BitEncodedInt(Column);
            writer.WriteOptionalString(File);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);
            EnvironmentVariableName = reader.ReadString();
            Line = reader.Read7BitEncodedInt();
            Column = reader.Read7BitEncodedInt();
            File = reader.ReadOptionalString() ?? string.Empty;
        }
    }
}
