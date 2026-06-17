// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;

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

            if (value is AbsolutePath absolutePath)
            {
                return absolutePath.Value ?? string.Empty;
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
    }
}
