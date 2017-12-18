// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;

namespace Microsoft.DotNet.Configurer
{
    public class NuGetCacheSentinel : INuGetCacheSentinel
    {
        public static readonly string SENTINEL = $"{Product.Version}.dotnetSentinel";
        public static readonly string INPROGRESS_SENTINEL = $"{Product.Version}.inprogress.dotnetSentinel";

        public bool UnauthorizedAccess { get; private set; }

        private readonly IFile _file;

        private readonly IDirectory _directory;

        private string _nugetCachePath;

        private string SentinelPath => Path.Combine(_nugetCachePath, SENTINEL);
        private string InProgressSentinelPath => Path.Combine(_nugetCachePath, INPROGRESS_SENTINEL);

        private Stream InProgressSentinel { get; set; }

        public NuGetCacheSentinel(CliFolderPathCalculator cliFolderPathCalculator) :
            this(cliFolderPathCalculator.CliFallbackFolderPath,
                 FileSystemWrapper.Default.File,
                 FileSystemWrapper.Default.Directory)
        {
        }

        internal NuGetCacheSentinel(string nugetCachePath, IFile file, IDirectory directory)
        {
            _nugetCachePath = nugetCachePath;
            _file = file;
            _directory = directory;

            SetInProgressSentinel();
        }

        public bool InProgressSentinelAlreadyExists()
        {
            return CouldNotGetAHandleToTheInProgressSentinel() && !UnauthorizedAccess;
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

        private bool CouldNotGetAHandleToTheInProgressSentinel()
        {
            return InProgressSentinel == null;
        }

        private void SetInProgressSentinel()
        {
            try
            {
                if (!_directory.Exists(_nugetCachePath))
                {
                    _directory.CreateDirectory(_nugetCachePath);
                }

                // open an exclusive handle to the in-progress sentinel and mark it for delete on close.
                // we open with exclusive FileShare.None access to indicate that the operation is in progress.
                // buffer size is minimum since we won't be reading or writing from the file.
                // delete on close is to indicate that the operation is no longer in progress when we dispose
                // this.
                InProgressSentinel = _file.OpenFile(
                    InProgressSentinelPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose);
            }
            catch (UnauthorizedAccessException)
            {
                UnauthorizedAccess = true;
            }
            catch { }
        }

        public void Dispose()
        {
            if (InProgressSentinel != null)
            {
                InProgressSentinel.Dispose();
            }
        }
    }
}
