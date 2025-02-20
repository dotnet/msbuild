// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the generated file used event
    /// </summary>
    internal class GeneratedFileUsedEventArgs : BuildMessageEventArgs
    {
        public GeneratedFileUsedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedFileUsedEventArgs"/> class.
        /// </summary>
        /// 
        public GeneratedFileUsedEventArgs(string filePath, string content)
        // We are not sending the event to binlog (just the file), so we do not want it
        // to have any stringified representation for other logs either.
            : base(string.Empty, null, null, MessageImportance.Low)
        {
            FilePath = filePath;
            Content = content;
        }

        /// <summary>
        /// The file path relative to the current project.
        /// </summary>
        public string? FilePath { set; get; }

        /// <summary>
        /// The content of the file.
        /// </summary>
        public string? Content { set; get; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            if (FilePath != null && Content != null)
            {
                writer.Write(FilePath);
                writer.Write(Content);
            }
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            FilePath = reader.ReadString();
            Content = reader.ReadString();
        }
    }
}
