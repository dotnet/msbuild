// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal static class ITaskItemExtensions
    {
        public static bool? GetBooleanMetadata(this ITaskItem item, string metadataName)
        {
            bool? result = null;

            string value = item.GetMetadata(metadataName);
            bool parsedResult;
            if (bool.TryParse(value, out parsedResult))
            {
                result = parsedResult;
            }

            return result;
        }
    }
}