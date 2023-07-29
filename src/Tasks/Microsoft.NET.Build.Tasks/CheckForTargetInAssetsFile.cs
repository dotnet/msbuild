// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForTargetInAssetsFile : TaskBase
    {
        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }


        protected override void ExecuteCore()
        {
            LockFile lockFile = new LockFileCache(this).GetLockFile(AssetsFilePath);

            lockFile.GetTargetAndThrowIfNotFound(TargetFramework, RuntimeIdentifier);
        }
    }
}
