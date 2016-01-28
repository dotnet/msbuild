// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NuGet
{
    public static class PackageIdValidator
    {
        private static readonly Regex _idRegex = new Regex(@"^\w+([_.-]\w+)*$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        internal const int MaxPackageIdLength = 100;

        public static bool IsValidPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException("packageId");
            }
            return _idRegex.IsMatch(packageId);
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