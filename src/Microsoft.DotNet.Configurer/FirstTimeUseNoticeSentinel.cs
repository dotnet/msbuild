// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;

namespace Microsoft.DotNet.Configurer
{
    public class FirstTimeUseNoticeSentinel : IFirstTimeUseNoticeSentinel
    {
        public static readonly string SENTINEL = $"{Product.Version}.dotnetFirstUseSentinel";

        private string _dotnetUserProfileFolderPath;
        private readonly IFileSystem _fileSystem;

        private string SentinelPath => Path.Combine(_dotnetUserProfileFolderPath, SENTINEL);

        public FirstTimeUseNoticeSentinel() :
            this(
                CliFolderPathCalculator.DotnetUserProfileFolderPath,
                FileSystemWrapper.Default)
        {
        }

        internal FirstTimeUseNoticeSentinel(string dotnetUserProfileFolderPath, IFileSystem fileSystem)
        {
            _dotnetUserProfileFolderPath = dotnetUserProfileFolderPath;
            _fileSystem = fileSystem;
        }

        public bool Exists()
        {
            return _fileSystem.File.Exists(SentinelPath);
        }

        public void CreateIfNotExists()
        {
            _fileSystem.CreateIfNotExists(SentinelPath);
        }

        public void Dispose()
        {
        }
    }
}
