// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    public static class TaskItemExtensions
    {
        public static bool TryGetMetadata(this ITaskItem item, string key, out string value)
        {
            value = item.GetMetadata(key);
            return !string.IsNullOrEmpty(value);
        }

        public static bool GetRequiredMetadata(this ITaskItem item, TaskLoggingHelper log, string key, out string value)
        {
            value = item.GetMetadata(key);
            if (string.IsNullOrEmpty(value))
            {
                log.LogError($"Item '{item.ItemSpec}' missing required metadata '{key}'.");
                return false;
            }

            return true;
        }
    }
}
