// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal class ResourceAssemblyInfo
    {
        public string Culture { get; }
        public string RelativePath { get; }

        public ResourceAssemblyInfo(string culture, string relativePath)
        {
            Culture = culture;
            RelativePath = relativePath;
        }

        public static ResourceAssemblyInfo CreateFromReferenceSatellitePath(ITaskItem referenceSatellitePath)
        {
            string destinationSubDirectory = referenceSatellitePath.GetMetadata("DestinationSubDirectory");

            string culture = destinationSubDirectory.Trim('\\', '/');
            string relativePath = Path.Combine(destinationSubDirectory, Path.GetFileName(referenceSatellitePath.ItemSpec));

            return new ResourceAssemblyInfo(culture, relativePath);
        }
    }
}
