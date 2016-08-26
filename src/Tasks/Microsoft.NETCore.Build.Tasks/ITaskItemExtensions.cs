// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

namespace Microsoft.NETCore.Build.Tasks
{
    internal static class ITaskItemExtensions
    {
        public static bool IsAnalyzer(this ITaskItem item)
        {
            var isAnalyzer = false;
            var analyzerString = item.GetMetadata(MetadataKeys.Analyzer);
            return bool.TryParse(analyzerString, out isAnalyzer) && isAnalyzer;
        }
    }
}
