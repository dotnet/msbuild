// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

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
        private Stream? _fileStream;
        private ZipArchive? _zipArchive;
        private readonly string _archiveFilePath;
        private readonly bool _runOnBackground;
        private const string DefaultSourcesArchiveExtension = ".ProjectImports.zip";

        /// <summary>
        /// Avoid visiting each file more than once.
        /// </summary>
        private readonly HashSet<string> _processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // this will form a chain of file write tasks, running sequentially on a background thread
        private Task _currentTask = Task.CompletedTask;

        internal static void FlushBlobToFile(
            string logFilePath,
            Stream contentStream)
        {
            string archiveFilePath = GetArchiveFilePath(logFilePath, DefaultSourcesArchiveExtension);

            using var fileStream = new FileStream(archiveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete);
            contentStream.CopyTo(fileStream);
        }

        // Archive file will be stored alongside the binlog
        private static string GetArchiveFilePath(string logFilePath, string sourcesArchiveExtension)
            => Path.ChangeExtension(logFilePath, sourcesArchiveExtension);

        public ProjectImportsCollector(
            string logFilePath,
            bool createFile,
            string sourcesArchiveExtension = DefaultSourcesArchiveExtension,
            bool runOnBackground = true)
        {
            if (createFile)
            {
                _archiveFilePath = GetArchiveFilePath(logFilePath, sourcesArchiveExtension);
            }
            else
            {
                string cacheDirectory = FileUtilities.GetCacheDirectory();
                if (!Directory.Exists(cacheDirectory))
                {
                    Directory.CreateDirectory(cacheDirectory);
                }

                // Archive file will be temporarily stored in MSBuild cache folder and deleted when no longer needed
                _archiveFilePath = Path.Combine(
                    cacheDirectory,
                    GetArchiveFilePath(
                        Path.GetFileName(logFilePath),
                        sourcesArchiveExtension));
            }

            try
            {
                _fileStream = new FileStream(_archiveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete);
                _zipArchive = new ZipArchive(_fileStream, ZipArchiveMode.Create);
            }
            catch
            {
                // For some reason we weren't able to create a file for the archive.
                // Disable the file collector.
                _fileStream = null;
                _zipArchive = null;
            }
            _runOnBackground = runOnBackground;
        }

        public void AddFile(string? filePath)
        {
            AddFileHelper(filePath, AddFileCore);
        }

        public void AddFileFromMemory(
            string? filePath,
            string data,
            DateTimeOffset? entryCreationStamp = null,
            bool makePathAbsolute = true)
        {
            AddFileHelper(filePath, path =>
                AddFileFromMemoryCore(path, data, makePathAbsolute, entryCreationStamp));
        }

        public void AddFileFromMemory(
            string? filePath,
            Stream data,
            DateTimeOffset? entryCreationStamp = null,
            bool makePathAbsolute = true)
        {
            AddFileHelper(filePath, path => AddFileFromMemoryCore(path, data, makePathAbsolute, entryCreationStamp));
        }

        private void AddFileHelper(
            string? filePath,
            Action<string> addFileWorker)
        {
            if (filePath != null && _fileStream != null)
            {
                lock (_fileStream)
                {
                    if (_runOnBackground)
                    {
                        // enqueue the task to add a file and return quickly
                        // to avoid holding up the current thread
                        _currentTask = _currentTask.ContinueWith(
                            t => { TryAddFile(); },
                            TaskScheduler.Default);
                    }
                    else
                    {
                        TryAddFile();
                    }
                }
            }

            bool TryAddFile()
            {
                try
                {
                    addFileWorker(filePath);
                    return true;
                }
                catch
                { }

                return false;
            }
        }

        /// <remarks>
        /// This method doesn't need locking/synchronization because it's only called
        /// from a task that is chained linearly
        /// </remarks>
        private void AddFileCore(string filePath)
        {
            // quick check to avoid repeated disk access for Exists etc.
            if (!ShouldAddFile(ref filePath, true, true))
            {
                return;
            }

            using FileStream content = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            AddFileData(filePath, content, null);
        }

        /// <remarks>
        /// This method doesn't need locking/synchronization because it's only called
        /// from a task that is chained linearly
        /// </remarks>
        private void AddFileFromMemoryCore(string filePath, string data, bool makePathAbsolute, DateTimeOffset? entryCreationStamp)
        {
            // quick check to avoid repeated disk access for Exists etc.
            if (!ShouldAddFile(ref filePath, false, makePathAbsolute))
            {
                return;
            }

            using var content = new MemoryStream(Encoding.UTF8.GetBytes(data));
            AddFileData(filePath, content, entryCreationStamp);
        }

        private void AddFileFromMemoryCore(string filePath, Stream data, bool makePathAbsolute, DateTimeOffset? entryCreationStamp)
        {
            // quick check to avoid repeated disk access for Exists etc.
            if (!ShouldAddFile(ref filePath, false, makePathAbsolute))
            {
                return;
            }

            AddFileData(filePath, data, entryCreationStamp);
        }

        private void AddFileData(string filePath, Stream data, DateTimeOffset? entryCreationStamp)
        {
            using Stream entryStream = OpenArchiveEntry(filePath, entryCreationStamp);
            data.CopyTo(entryStream);
        }

        private bool ShouldAddFile(ref string filePath, bool checkFileExistence, bool makeAbsolute)
        {
            // quick check to avoid repeated disk access for Exists etc.
            if (_processedFiles.Contains(filePath))
            {
                return false;
            }

            if (checkFileExistence && !File.Exists(filePath))
            {
                _processedFiles.Add(filePath);
                return false;
            }

            // Only make the path absolute if it's request. In the replay scenario, the file entries
            // are read from zip archive - where ':' is stripped and path can then seem relative.
            if (makeAbsolute)
            {
                filePath = Path.GetFullPath(filePath);
            }

            // if the file is already included, don't include it again
            return _processedFiles.Add(filePath);
        }

        private Stream OpenArchiveEntry(string filePath, DateTimeOffset? entryCreationStamp)
        {
            string archivePath = CalculateArchivePath(filePath);
            var archiveEntry = _zipArchive!.CreateEntry(archivePath);
            if (entryCreationStamp.HasValue)
            {
                archiveEntry.LastWriteTime = entryCreationStamp.Value;
            }

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

        public void ProcessResult(Action<Stream> consumeStream, Action<string> onError)
        {
            Close();

            // It is possible that the archive couldn't be created for some reason.
            // Only embed it if it actually exists.
            if (FileSystems.Default.FileExists(_archiveFilePath))
            {
                using FileStream fileStream = File.OpenRead(_archiveFilePath);

                if (fileStream.Length > int.MaxValue)
                {
                    onError(ResourceUtilities.GetResourceString("Binlog_ImportFileSizeError"));
                }
                else
                {
                    consumeStream(fileStream);
                }
            }
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

        public void DeleteArchive()
        {
            Close();
            File.Delete(_archiveFilePath);
        }
    }
}
