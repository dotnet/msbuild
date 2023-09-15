// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Microsoft.Build.Logging
{
    public class ArchiveFile
    {
        public ArchiveFile(string fullPath, Stream contentStream)
        {
            FullPath = fullPath;
            // We need to specify encoding without preamble - as then StreamReader will
            //  automatically adjust the encoding to match the preamble (if present).
            _contentReader = new StreamReader(contentStream, new System.Text.UTF8Encoding(false));
        }

        public ArchiveFile(string fullPath, string content, Encoding? contentEncoding = null)
        {
            FullPath = fullPath;
            _content = content;
            _stringAcquired = true;
            _contentReader = StreamReader.Null;
            _stringEncoding = contentEncoding ?? Encoding.UTF8;
        }

        public static ArchiveFile From(ZipArchiveEntry entry)
        {
            return new ArchiveFile(entry.FullName, entry.Open());
        }

        public string FullPath { get; }

        public Encoding Encoding => _stringEncoding ?? _contentReader.CurrentEncoding;

        public bool CanUseReader => !_stringAcquired;
        public bool CanUseString => !_streamAcquired;

        public StreamReader GetContentReader()
        {
            if (_stringAcquired)
            {
                throw new InvalidOperationException("Content already acquired as string via GetContent or initialized as string only.");
            }

            _streamAcquired = true;
            return _contentReader;
        }

        public string GetContent()
        {
            if (_streamAcquired)
            {
                throw new InvalidOperationException("Content already acquired as StreamReader via GetContnetReader.");
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
        private readonly Encoding? _stringEncoding;

        // Intentionally not exposing this publicly (e.g. as IDisposable implementation)
        // as we don't want to user to be bothered with ownership and disposing concerns.
        internal void Dispose() => _contentReader.Dispose();
    }
}
