// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Abstractions;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    internal static class ApiCompatRunnerExtensions
    {
        public static void QueueApiCompatFromContentItem(this ApiCompatRunner apiCompatRunner, string packageId, ContentItem leftItem, ContentItem rightItem, string header, bool isBaseline = false)
        {
            string? displayString = isBaseline ? Resources.Baseline + " " + leftItem.Path : null;
            MetadataInformation left = new(packageId, ((NuGetFramework)leftItem.Properties["tfm"]).GetShortFolderName(), leftItem.Path, displayString);
            MetadataInformation right = new(packageId, ((NuGetFramework)rightItem.Properties["tfm"]).GetShortFolderName(), rightItem.Path);

            apiCompatRunner.QueueApiCompat(left, right, header);
        }
    }
}
