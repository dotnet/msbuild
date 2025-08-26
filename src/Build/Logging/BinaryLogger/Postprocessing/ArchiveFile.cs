// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// An object model for binlog embedded files.
    /// Used in <see cref="IBuildEventArgsReaderNotifications.ArchiveFileEncountered"/> event.
    /// </summary>
    public abstract class ArchiveData : IDisposable
    {
        private protected ArchiveData(string fullPath) => FullPath = fullPath;

        /// <summary>
        /// Full path of the original file before it was put in the embedded archive.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Materializes the whole content of the embedded file in memory as a string.
        /// </summary>
        /// <returns></returns>
        public abstract ArchiveFile ToArchiveFile();

        public virtual void Dispose()
        { }
    }

    /// <summary>
    /// Fully materialized (in-memory) embedded file.
    /// Easier to work with (the content is expressed in a single string), but more memory greedy.
    /// </summary>
    public sealed class ArchiveFile : ArchiveData
    {
        public ArchiveFile(string fullPath, string content)
            : base(fullPath)
            => Content = content;

        /// <summary>
        /// The content of the original file.
        /// </summary>
        public string Content { get; }

        /// <inheritdoc cref="ArchiveData.ToArchiveFile" />
        public override ArchiveFile ToArchiveFile()
            => this;
    }

    /// <summary>
    /// Lazy (streaming) embedded file.
    /// Might be favorable for large files, as it doesn't materialize the whole content in memory.
    /// </summary>
    public sealed class ArchiveStream : ArchiveData
    {
        public ArchiveStream(string fullPath, StreamReader contentReader)
            : base(fullPath)
            => ContentReader = contentReader;

        /// <summary>
        /// Stream over the content of the archived file.
        /// </summary>
        public StreamReader ContentReader { get; }

        /// <summary>
        /// Creates an externally exposable embedded file representation from a <see cref="ZipArchiveEntry"/> (which is an implementation detail currently).
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        internal static ArchiveStream From(ZipArchiveEntry entry)
        {
            return new ArchiveStream(entry.FullName, new StreamReader(entry.Open()));
        }

        /// <inheritdoc cref="ArchiveData.ToArchiveFile" />
        public override ArchiveFile ToArchiveFile()
        {
            var content = ContentReader.ReadToEnd();
            ContentReader.Dispose();
            return new ArchiveFile(FullPath, content);
        }

        public override void Dispose()
            => ContentReader.Dispose();
    }
}
