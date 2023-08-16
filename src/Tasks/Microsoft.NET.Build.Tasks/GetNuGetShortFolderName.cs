// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
