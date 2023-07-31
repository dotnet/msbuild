// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
