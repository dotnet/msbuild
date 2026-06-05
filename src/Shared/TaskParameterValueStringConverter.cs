// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;

namespace Microsoft.Build.Shared
{
    internal static class TaskParameterValueStringConverter
    {
        internal static string ToString(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (TryGetAbsolutePathValue(value, out string absolutePathValue))
            {
                return absolutePathValue;
            }

            if (value is FileInfo fileInfo)
            {
                return fileInfo.FullName;
            }

            if (value is DirectoryInfo directoryInfo)
            {
                return directoryInfo.FullName;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool TryGetAbsolutePathValue(object value, out string absolutePathValue)
        {
            absolutePathValue = string.Empty;

            if (!TaskItemTypeHelper.IsAbsolutePathType(value.GetType()))
            {
                return false;
            }

            object? rawValue = value.GetType().GetProperty("Value")?.GetValue(value, null);
            absolutePathValue = rawValue as string ?? string.Empty;
            return true;
        }
    }
}
