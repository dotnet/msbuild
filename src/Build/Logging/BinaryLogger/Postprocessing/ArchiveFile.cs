// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Logging
{
    public class ArchiveFile : IDisposable
    {
        public ArchiveFile(string fullPath, StreamReader contentReader)
        {
            FullPath = fullPath;
            _contentReader = contentReader;
        }

        public static ArchiveFile From(ZipArchiveEntry entry)
        {
            return new ArchiveFile(CalculateArchivePath(entry.FullName), new StreamReader(entry.Open()));
        }

        public string FullPath { get; }

        public StreamReader GetContentReader()
        {
            if (_stringAcquired)
            {
                throw new InvalidOperationException("Content already acquired as string via GetContent.");
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

        public static string CalculateArchivePath(string filePath)
        {
            string archivePath = filePath;

            if (filePath.Contains(":") || (!filePath.StartsWith("\\") && !filePath.StartsWith("/")))
            {
                archivePath = archivePath.Replace(":", "");
                archivePath = archivePath.Replace("/", "\\");
                archivePath = archivePath.Replace("\\\\", "\\");
            }
            else
            {
                archivePath = archivePath.Replace("\\", "/");
                archivePath = archivePath.Replace("//", "/");
            }

            return archivePath;
        }

        public void Dispose() => _contentReader.Dispose();
    }
}
