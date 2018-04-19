// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForTargetInAssetsFile : TaskBase
    {
        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFrameworkMoniker { get; set; }

        public string RuntimeIdentifier { get; set; }


        protected override void ExecuteCore()
        {
            LockFile lockFile = new LockFileCache(this).GetLockFile(AssetsFilePath);

            var nugetFramework = NuGetUtils.ParseFrameworkName(TargetFrameworkMoniker);

            lockFile.GetTargetAndThrowIfNotFound(nugetFramework, RuntimeIdentifier);
        }
    }
}
