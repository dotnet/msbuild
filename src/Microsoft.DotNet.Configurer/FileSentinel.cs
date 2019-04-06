// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class FileSentinel : IFileSentinel
    {
        private readonly FilePath _file;
        private readonly IFileSystem _fileSystem;

        public FileSentinel(FilePath file) :
            this(file, fileSystem: null)
        {
        }

        internal FileSentinel(FilePath file, IFileSystem fileSystem)
        {
            _file = file;
            _fileSystem = fileSystem ?? FileSystemWrapper.Default;
        }

        public bool Exists()
        {
            return _fileSystem.File.Exists(_file.Value);
        }

        public void Create()
        {
            _fileSystem.CreateIfNotExists(_file.Value);
        }
    }
}
