// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class GetNuGetShortFolderName : TaskBase
    {
        [Required]
        public string TargetFrameworkMoniker { get; set; }
        
        public string TargetPlatformMoniker { get; set; }


        [Output]
        public string NuGetShortFolderName { get; set; }

        protected override void ExecuteCore()
        {
            NuGetShortFolderName = NuGetFramework
                .ParseComponents(TargetFrameworkMoniker, TargetPlatformMoniker)
                .GetShortFolderName();
        }
    }
}
