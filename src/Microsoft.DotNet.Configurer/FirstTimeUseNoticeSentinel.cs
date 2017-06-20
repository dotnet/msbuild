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

        private readonly IFile _file;

        private string _nugetCachePath;

        private string SentinelPath => Path.Combine(_nugetCachePath, SENTINEL);

        public FirstTimeUseNoticeSentinel(CliFallbackFolderPathCalculator cliFallbackFolderPathCalculator) :
            this(cliFallbackFolderPathCalculator.CliFallbackFolderPath, FileSystemWrapper.Default.File)
        {
        }

        internal FirstTimeUseNoticeSentinel(string nugetCachePath, IFile file)
        {
            _file = file;
            _nugetCachePath = nugetCachePath;
        }

        public bool Exists()
        {
            return _file.Exists(SentinelPath);
        }

        public void CreateIfNotExists()
        {
            if (!Exists())
            {
                _file.CreateEmptyFile(SentinelPath);
            }
        }

        public void Dispose()
        {
        }
    }
}
