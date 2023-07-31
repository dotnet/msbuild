// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
