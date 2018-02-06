// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Tools.Test.Utilities.Mock;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.Extensions.DependencyModel.Tests
{
    class FileSystemMockBuilder
    {
        private Dictionary<string, string> _files = new Dictionary<string, string>();

        public string TemporaryFolder { get; set; }

        internal static IFileSystem Empty { get; } = Create().Build();

        public static FileSystemMockBuilder Create()
        {
            return new FileSystemMockBuilder();
        }

        public FileSystemMockBuilder AddFile(string name, string content = "")
        {
            _files.Add(name, content);
            return this;
        }

        public FileSystemMockBuilder AddFiles(string basePath, params string[] files)
        {
            foreach (var file in files)
            {
                AddFile(Path.Combine(basePath, file));
            }
            return this;
        }

        internal IFileSystem Build()
        {
            return new FileSystemMock(_files, TemporaryFolder);
        }

        private class FileSystemMock : IFileSystem
        {
            public FileSystemMock(Dictionary<string, string> files, string temporaryFolder)
            {
                File = new FileMock(files);
                Directory = new DirectoryMock(files, temporaryFolder);
            }

            public IFile File { get; }

            public IDirectory Directory { get; }
        }

        private class FileMock : IFile
        {
            private Dictionary<string, string> _files;
            
            public FileMock(Dictionary<string, string> files)
            {
                _files = files;
            }

            public bool Exists(string path)
            {
                return _files.ContainsKey(path);
            }

            public string ReadAllText(string path)
            {
                string text;
                if (!_files.TryGetValue(path, out text))
                {
                    throw new FileNotFoundException(path);
                }
                return text;
            }

            public Stream OpenRead(string path)
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(ReadAllText(path)));
            }

            public Stream OpenFile(
                string path,
                FileMode fileMode,
                FileAccess fileAccess,
                FileShare fileShare,
                int bufferSize,
                FileOptions fileOptions)
            {
                throw new NotImplementedException();
            }

            public void CreateEmptyFile(string path)
            {
                _files.Add(path, string.Empty);
            }

            public void WriteAllText(string path, string content)
            {
                _files[path] = content;
            }

            public void Delete(string path)
            {
                if (!Exists(path))
                {
                    return;
                }

                _files.Remove(path);
            }
        }

        private class DirectoryMock : IDirectory
        {
            private Dictionary<string, string> _files;
            private readonly TemporaryDirectoryMock _temporaryDirectory;

            public DirectoryMock(Dictionary<string, string> files, string temporaryDirectory)
            {
                _files = files;
                _temporaryDirectory = new TemporaryDirectoryMock(temporaryDirectory);
            }

            public ITemporaryDirectory CreateTemporaryDirectory()
            {
                return _temporaryDirectory;
            }

            public IEnumerable<string> GetFiles(string path, string searchPattern)
            {
                throw new NotImplementedException();
            }

            public string GetDirectoryFullName(string path)
            {
                throw new NotImplementedException();
            }

            public bool Exists(string path)
            {
                return _files.Keys.Any(k => k.StartsWith(path));
            }

            public void CreateDirectory(string path)
            {
                _files.Add(path, path);
            }

            public void Delete(string path, bool recursive)
            {
                if (!recursive && Exists(path) == true)
                {
                    if (_files.Keys.Where(k => k.StartsWith(path)).Count() > 1)
                    {
                        throw new IOException("The directory is not empty");
                    }
                }

                foreach (var k in _files.Keys.Where(k => k.StartsWith(path)).ToList())
                {
                    _files.Remove(k);
                }
            }
        }

        private class TemporaryDirectoryMock : ITemporaryDirectoryMock
        {
            public bool DisposedTemporaryDirectory { get; private set; }

            public TemporaryDirectoryMock(string temporaryDirectory)
            {
                DirectoryPath = temporaryDirectory;
            }

            public string DirectoryPath { get; }

            public void Dispose()
            {
                DisposedTemporaryDirectory = true;
            }
        }
    }

}
