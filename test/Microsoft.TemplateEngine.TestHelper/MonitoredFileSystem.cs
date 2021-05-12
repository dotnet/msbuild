// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class MonitoredFileSystem : IPhysicalFileSystem
    {
        private readonly IPhysicalFileSystem _baseFileSystem;
        private List<DirectoryScanParameters> _directoriesScanned = new List<DirectoryScanParameters>();
        private List<string> _filesOpened = new List<string>();

        public MonitoredFileSystem(IPhysicalFileSystem baseFileSystem)
        {
            _baseFileSystem = baseFileSystem;
        }

        public IReadOnlyList<DirectoryScanParameters> DirectoriesScanned => _directoriesScanned;

        public IReadOnlyList<string> FilesOpened => _filesOpened;

        public void CreateDirectory(string path) => _baseFileSystem.CreateDirectory(path);

        public Stream CreateFile(string path) => _baseFileSystem.CreateFile(path);

        public void DirectoryDelete(string path, bool recursive) => _baseFileSystem.DirectoryDelete(path, recursive);

        public bool DirectoryExists(string directory) => _baseFileSystem.DirectoryExists(directory);

        public IEnumerable<string> EnumerateDirectories(string path, string pattern, SearchOption searchOption) => _baseFileSystem.EnumerateDirectories(path, pattern, searchOption);

        public IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption searchOption) => _baseFileSystem.EnumerateFiles(path, pattern, searchOption);

        public IEnumerable<string> EnumerateFileSystemEntries(string directoryName, string pattern, SearchOption searchOption)
        {
            RecordDirectoryScan(directoryName, pattern, searchOption);
            return _baseFileSystem.EnumerateFileSystemEntries(directoryName, pattern, searchOption);
        }

        public void Reset()
        {
            _directoriesScanned.Clear();
            _filesOpened.Clear();
        }

        public void FileCopy(string sourcePath, string targetPath, bool overwrite) => _baseFileSystem.FileCopy(sourcePath, targetPath, overwrite);

        public void FileDelete(string path) => _baseFileSystem.FileDelete(path);

        public bool FileExists(string file) => _baseFileSystem.FileExists(file);

        public string GetCurrentDirectory() => _baseFileSystem.GetCurrentDirectory();

        public FileAttributes GetFileAttributes(string file) => _baseFileSystem.GetFileAttributes(file);

        public Stream OpenRead(string path)
        {
            _filesOpened.Add(path);
            return _baseFileSystem.OpenRead(path);
        }

        public string ReadAllText(string path) => _baseFileSystem.ReadAllText(path);

        public byte[] ReadAllBytes(string path) => _baseFileSystem.ReadAllBytes(path);

        public void SetFileAttributes(string file, FileAttributes attributes) => _baseFileSystem.SetFileAttributes(file, attributes);

        public void WriteAllText(string path, string value) => _baseFileSystem.WriteAllText(path, value);

        public IDisposable WatchFileChanges(string filepath, FileSystemEventHandler fileChanged) => _baseFileSystem.WatchFileChanges(filepath, fileChanged);

        public DateTime GetLastWriteTimeUtc(string file) => _baseFileSystem.GetLastWriteTimeUtc(file);

        public void SetLastWriteTimeUtc(string file, DateTime lastWriteTimeUtc) => _baseFileSystem.SetLastWriteTimeUtc(file, lastWriteTimeUtc);

        private void RecordDirectoryScan(string directoryName, string pattern, SearchOption searchOption)
        {
            _directoriesScanned.Add(new DirectoryScanParameters
            {
                DirectoryName = directoryName,
                Pattern = pattern,
                SearchOption = searchOption
            });
        }

        public class DirectoryScanParameters
        {
            public string? DirectoryName { get; set; }

            public string? Pattern { get; set; }

            public SearchOption SearchOption { get; set; }
        }
    }
}
