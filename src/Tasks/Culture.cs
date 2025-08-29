﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Utility functions for dealing with Culture information.
    /// </summary>
    internal static class Culture
    {
        /// <summary>
        /// Culture information about an item.
        /// </summary>
        internal struct ItemCultureInfo
        {
            internal string culture;
            internal string cultureNeutralFilename;
        };

        /// <summary>
        /// Given an item's filename, return information about the item including the culture and the culture-neutral filename.
        /// </summary>
        /// <remarks>
        /// We've decided to ignore explicit Culture attributes on items.
        /// </remarks>
        internal static ItemCultureInfo GetItemCultureInfo(
            string name,
            string dependentUponFilename,
            bool treatAsCultureNeutral = false)
        {
            ItemCultureInfo info;
            info.culture = null;
            string parentName = dependentUponFilename ?? String.Empty;

            if (treatAsCultureNeutral || string.Equals(Path.GetFileNameWithoutExtension(parentName),
                                   Path.GetFileNameWithoutExtension(name),
                                   StringComparison.OrdinalIgnoreCase))
            {
                // Dependent but we treat it is as not localized because they have same base filename
                // Or we want to treat this as a 'culture-neutral' file and retain the culture in the name. https://github.com/dotnet/msbuild/pull/5824
                info.cultureNeutralFilename = name;
            }
            else
            {
                // Either not dependent on another file, or it has a distinct base filename

                // If the item is defined as "Strings.en-US.resx", then ...

                // ... base file name will be "Strings.en-US" ...
                string baseFileNameWithCulture = Path.GetFileNameWithoutExtension(name);

                // ... and cultureName will be ".en-US".
                string cultureName = Path.GetExtension(baseFileNameWithCulture);

                // See if this is a valid culture name.
                bool validCulture = false;
                if ((cultureName?.Length > 1))
                {
                    // ... strip the "." to make "en-US"
                    cultureName = cultureName.Substring(1);
                    validCulture = CultureInfoCache.IsValidCultureString(cultureName);
                }

                if (validCulture)
                {
                    // A valid culture was found.
                    info.culture = cultureName;

                    // Copy the assigned file and make it culture-neutral
                    string extension = Path.GetExtension(name);
                    string baseFileName = Path.GetFileNameWithoutExtension(baseFileNameWithCulture);
                    string baseFolder = Path.GetDirectoryName(name);
                    string fileName = baseFileName + extension;
                    info.cultureNeutralFilename = Path.Combine(baseFolder, fileName);
                }
                else
                {
                    // No valid culture was found. In this case, the culture-neutral
                    // name is the just the original file name.
                    info.cultureNeutralFilename = name;
                }
            }

            return info;
        }
    }
}
