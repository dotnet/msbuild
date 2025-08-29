﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Creates a zip archive with all the .csproj and .targets encountered during the build.
    /// The internal .zip file structure matches closely the layout of the original sources on disk.
    /// The .zip file can be used to correlate the file names and positions in the build log file with the
    /// actual sources.
    /// </summary>
    internal class ProjectImportsCollector
    {
        private Stream _fileStream;
        private ZipArchive _zipArchive;

        public string ArchiveFilePath { get; }

        /// <summary>
        /// Avoid visiting each file more than once.
        /// </summary>
        private readonly HashSet<string> _processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // this will form a chain of file write tasks, running sequentially on a background thread
        private Task _currentTask = Task.CompletedTask;

        public ProjectImportsCollector(string logFilePath, bool createFile, string sourcesArchiveExtension = ".ProjectImports.zip")
        {
            if (createFile)
            {
                // Archive file will be stored alongside the binlog
                ArchiveFilePath = Path.ChangeExtension(logFilePath, sourcesArchiveExtension);
            }
            else
            {
                string cacheDirectory = FileUtilities.GetCacheDirectory();
                if (!Directory.Exists(cacheDirectory))
                {
                    Directory.CreateDirectory(cacheDirectory);
                }

                // Archive file will be temporarily stored in MSBuild cache folder and deleted when no longer needed
                ArchiveFilePath = Path.Combine(
                    cacheDirectory,
                    Path.ChangeExtension(
                        Path.GetFileName(logFilePath),
                        sourcesArchiveExtension));
            }

            try
            {
                _fileStream = new FileStream(ArchiveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete);
                _zipArchive = new ZipArchive(_fileStream, ZipArchiveMode.Create);
            }
            catch
            {
                // For some reason we weren't able to create a file for the archive.
                // Disable the file collector.
                _fileStream = null;
                _zipArchive = null;
            }
        }

        public void AddFile(string filePath)
        {
            if (filePath != null && _fileStream != null)
            {
                lock (_fileStream)
                {
                    // enqueue the task to add a file and return quickly
                    // to avoid holding up the current thread
                    _currentTask = _currentTask.ContinueWith(t =>
                    {
                        try
                        {
                            AddFileCore(filePath);
                        }
                        catch
                        {
                        }
                    }, TaskScheduler.Default);
                }
            }
        }

        public void AddFileFromMemory(string filePath, string data)
        {
            if (filePath != null && data != null && _fileStream != null)
            {
                lock (_fileStream)
                {
                    // enqueue the task to add a file and return quickly
                    // to avoid holding up the current thread
                    _currentTask = _currentTask.ContinueWith(t =>
                    {
                        try
                        {
                            AddFileFromMemoryCore(filePath, data);
                        }
                        catch
                        {
                        }
                    }, TaskScheduler.Default);
                }
            }
        }

        /// <remarks>
        /// This method doesn't need locking/synchronization because it's only called
        /// from a task that is chained linearly
        /// </remarks>
        private void AddFileCore(string filePath)
        {
            // quick check to avoid repeated disk access for Exists etc.
            if (_processedFiles.Contains(filePath))
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                _processedFiles.Add(filePath);
                return;
            }

            filePath = Path.GetFullPath(filePath);

            // if the file is already included, don't include it again
            if (!_processedFiles.Add(filePath))
            {
                return;
            }

            using FileStream content = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using Stream entryStream = OpenArchiveEntry(filePath);
            content.CopyTo(entryStream);
        }

        /// <remarks>
        /// This method doesn't need locking/synchronization because it's only called
        /// from a task that is chained linearly
        /// </remarks>
        private void AddFileFromMemoryCore(string filePath, string data)
        {
            // quick check to avoid repeated disk access for Exists etc.
            if (_processedFiles.Contains(filePath))
            {
                return;
            }

            filePath = Path.GetFullPath(filePath);

            // if the file is already included, don't include it again
            if (!_processedFiles.Add(filePath))
            {
                return;
            }

            using (Stream entryStream = OpenArchiveEntry(filePath))
            using (var content = new MemoryStream(Encoding.UTF8.GetBytes(data)))
            {
                content.CopyTo(entryStream);
            }
        }

        private Stream OpenArchiveEntry(string filePath)
        {
            string archivePath = CalculateArchivePath(filePath);
            var archiveEntry = _zipArchive.CreateEntry(archivePath);
            return archiveEntry.Open();
        }

        private static string CalculateArchivePath(string filePath)
        {
            string archivePath = filePath;

            archivePath = archivePath.Replace(":", "");
            archivePath = archivePath.Replace("\\\\", "\\");
            archivePath = archivePath.Replace("/", "\\");

            return archivePath;
        }

        public void Close()
        {
            // wait for all pending file writes to complete
            _currentTask.Wait();

            if (_zipArchive != null)
            {
                _zipArchive.Dispose();
                _zipArchive = null;
            }

            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }
    }
}
