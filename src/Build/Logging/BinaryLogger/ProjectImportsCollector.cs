using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

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
        private FileStream _fileStream;
        private ZipArchive _zipArchive;

        public string ArchiveFilePath { get; set; }

        /// <summary>
        /// Avoid visiting each file more than once.
        /// </summary>
        private readonly HashSet<string> _processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // this will form a chain of file write tasks, running sequentially on a background thread
        private Task _currentTask = Task.CompletedTask;

        public ProjectImportsCollector(string logFilePath, string sourcesArchiveExtension = ".ProjectImports.zip")
        {
            ArchiveFilePath = Path.ChangeExtension(logFilePath, sourcesArchiveExtension);

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

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
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

            using (Stream entryStream = OpenArchiveEntry(filePath, fileInfo.LastWriteTime))
            using (FileStream content = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                content.CopyTo(entryStream);
            }
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

            using (Stream entryStream = OpenArchiveEntry(filePath, DateTime.UtcNow))
            using (var content = new MemoryStream(Encoding.UTF8.GetBytes(data)))
            {
                content.CopyTo(entryStream);
            }
        }

        private Stream OpenArchiveEntry(string filePath, DateTime lastWriteTime)
        {
            string archivePath = CalculateArchivePath(filePath);
            var archiveEntry = _zipArchive.CreateEntry(archivePath);
            archiveEntry.LastWriteTime = lastWriteTime;
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
