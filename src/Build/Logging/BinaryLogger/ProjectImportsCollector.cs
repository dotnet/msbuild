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
        private Stream _stream;
        public byte[] GetAllBytes()
        {
            if (_stream == null)
            {
                return Array.Empty<byte>();
            }
            else if (ArchiveFilePath == null)
            {
                var stream = _stream as MemoryStream;
                // Before we can use the zip archive, it must be closed.
                Close(false);
                return stream.ToArray();
            }
            else
            {
                Close();
                return File.ReadAllBytes(ArchiveFilePath);
            }
        }

        private ZipArchive _zipArchive;

        private string ArchiveFilePath { get; set; }

        /// <summary>
        /// Avoid visiting each file more than once.
        /// </summary>
        private readonly HashSet<string> _processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // this will form a chain of file write tasks, running sequentially on a background thread
        private Task _currentTask = Task.CompletedTask;

        public ProjectImportsCollector(string logFilePath, bool createFile, string sourcesArchiveExtension = ".ProjectImports.zip")
        {
            try
            {
                if (createFile)
                {
                    ArchiveFilePath = Path.ChangeExtension(logFilePath, sourcesArchiveExtension);
                    _stream = new FileStream(ArchiveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete);
                }
                else
                {
                    _stream = new MemoryStream();
                }
                _zipArchive = new ZipArchive(_stream, ZipArchiveMode.Create, true);
            }
            catch
            {
                // For some reason we weren't able to create a file for the archive.
                // Disable the file collector.
                _stream = null;
                _zipArchive = null;
            }
        }

        public void AddFile(string filePath)
        {
            if (filePath != null && _stream != null)
            {
                lock (_stream)
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
            if (filePath != null && data != null && _stream != null)
            {
                lock (_stream)
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

            using (Stream entryStream = OpenArchiveEntry(filePath))
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

        public void Close(bool closeStream = true)
        {
            // wait for all pending file writes to complete
            _currentTask.Wait();

            if (_zipArchive != null)
            {
                _zipArchive.Dispose();
                _zipArchive = null;
            }

            if (closeStream && (_stream != null))
            {
                _stream.Dispose();
                _stream = null;
            }
        }
    }
}
