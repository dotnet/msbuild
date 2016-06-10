// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.ProjectModel.Resolution;

namespace Microsoft.DotNet.Configurer
{
    public class NuGetCacheSentinel : INuGetCacheSentinel
    {
        public static readonly string SENTINEL = $"{Product.Version}.dotnetSentinel";
        public static readonly string INPROGRESS_SENTINEL = $"{Product.Version}.inprogress.dotnetSentinel";

        private readonly IFile _file;

        private string _nugetCachePath;

        private string NuGetCachePath
        {
            get
            {
                if (string.IsNullOrEmpty(_nugetCachePath))
                {
                    _nugetCachePath = PackageDependencyProvider.ResolvePackagesPath(null, null);
                }

                return _nugetCachePath;
            }
        }

        private string SentinelPath => Path.Combine(NuGetCachePath, SENTINEL);
        private string InProgressSentinelPath => Path.Combine(NuGetCachePath, INPROGRESS_SENTINEL);

        private Stream InProgressSentinel { get; set; }

        public NuGetCacheSentinel() : this(string.Empty, FileSystemWrapper.Default.File)
        {
        }

        internal NuGetCacheSentinel(string nugetCachePath, IFile file)
        {
            _file = file;
            _nugetCachePath = nugetCachePath;

            SetInProgressSentinel();
        }

        public bool InProgressSentinelAlreadyExists()
        {
            return CouldNotGetAHandleToTheInProgressSentinel();
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
