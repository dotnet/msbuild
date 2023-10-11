// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    public sealed class ArchiveFile
    {
        public ArchiveFile(string fullPath, Stream contentStream)
        {
            FullPath = fullPath;
            _contentReader = new StreamReader(contentStream);
        }

        public ArchiveFile(string fullPath, string content)
        {
            FullPath = fullPath;
            _content = content;
            _stringAcquired = true;
            _contentReader = StreamReader.Null;
        }

        internal static ArchiveFile From(ZipArchiveEntry entry)
        {
            return new ArchiveFile(entry.FullName, entry.Open());
        }

        public string FullPath { get; }
        public bool CanUseReader => !_stringAcquired;
        public bool CanUseString => !_streamAcquired;

        /// <summary>
        /// Fetches the file content as a stream reader (forward only).
        /// This prevents the content to be read as string.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public StreamReader GetContentReader()
        {
            if (_stringAcquired)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_ArchiveFile_AcquiredAsString"));
            }

            _streamAcquired = true;
            return _contentReader;
        }

        /// <summary>
        /// Fetches the file content as a string.
        /// This prevents the content to be fetched via StreamReader.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public string GetContent()
        {
            if (_streamAcquired)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_ArchiveFile_AcquiredAsStream"));
            }

            if (!_stringAcquired)
            {
                _stringAcquired = true;
                _content = _contentReader.ReadToEnd();
            }

            return _content!;
        }

        private bool _streamAcquired;
        private bool _stringAcquired;
        private readonly StreamReader _contentReader;
        private string? _content;

        // Intentionally not exposing this publicly (e.g. as IDisposable implementation)
        // as we don't want to user to be bothered with ownership and disposing concerns.
        internal void Dispose() => _contentReader.Dispose();
    }
}
