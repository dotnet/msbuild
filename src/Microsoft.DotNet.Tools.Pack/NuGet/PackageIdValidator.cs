// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace NuGet
{
    public static class PackageIdValidator
    {
        internal const int MaxPackageIdLength = 100;

        public static bool IsValidPackageId(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException(nameof(packageId));
            }

            // Rules: 
            // Should start with a character
            // Can be followed by '.' or '-'. Cannot have 2 of these special characters consecutively. 
            // Cannot end with '-' or '.'

            var firstChar = packageId[0];
            if (!char.IsLetterOrDigit(firstChar) && firstChar != '_')
            {
                // Should start with a char/digit/_.
                return false;
            }

            var lastChar = packageId[packageId.Length - 1];
            if (lastChar == '-' || lastChar == '.')
            {
                // Should not end with a '-' or '.'.
                return false;
            }

            for (int index = 1; index < packageId.Length - 1; index++)
            {
                var ch = packageId[index];
                if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '.')
                {
                    return false;
                }

                if ((ch == '-' || ch == '.') && ch == packageId[index - 1])
                {
                    // Cannot have two successive '-' or '.' in the name.
                    return false;
                }
            }

            return true;
        }

        public static void ValidatePackageId(string packageId)
        {
            if (packageId.Length > MaxPackageIdLength)
            {
                // TODO: Resources
                throw new ArgumentException("NuGetResources.Manifest_IdMaxLengthExceeded");
            }

            if (!IsValidPackageId(packageId))
            {
                // TODO: Resources
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "NuGetResources.InvalidPackageId", packageId));
            }
        }
    }
}