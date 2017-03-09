// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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

        public static bool HasMetadataValue(this ITaskItem item, string name, string expectedValue)
        {
            string value = item.GetMetadata(name);

            return string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
