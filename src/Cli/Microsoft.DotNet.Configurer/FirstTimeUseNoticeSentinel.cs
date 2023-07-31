// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class FirstTimeUseNoticeSentinel : IFirstTimeUseNoticeSentinel
    {
        public static readonly string SENTINEL = $"{Product.Version}.dotnetFirstUseSentinel";

        private readonly string _dotnetUserProfileFolderPath;
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
