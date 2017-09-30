// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Tasks;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    static class FrameworkListReader
    {
        public static IEnumerable<ConflictItem> LoadConflictItems(string frameworkListPath, ILog log)
        {
            if (frameworkListPath == null)
            {
                throw new ArgumentNullException(nameof(frameworkListPath));
            }

            if (!File.Exists(frameworkListPath))
            {
                //  This is not an error, as we get both the root target framework directory as well as the Facades folder passed in as TargetFrameworkDirectories.
                //  Only the root will have a RedistList\FrameworkList.xml in it
                yield break;
            }

            var frameworkList = XDocument.Load(frameworkListPath);
            foreach (var file in frameworkList.Elements("File"))
            {
                var assemblyName = file.Attribute("AssemblyName")?.Value;
                var assemblyVersionString = file.Attribute("Version")?.Value;

                if (string.IsNullOrEmpty(assemblyName))
                {
                    string errorMessage = string.Format(CultureInfo.InvariantCulture, Strings.ErrorParsingFrameworkListInvalidValue,
                        frameworkListPath,
                        "AssemblyName",
                        assemblyName);
                    log.LogError(errorMessage);
                    yield break;
                }

                Version assemblyVersion;
                if (string.IsNullOrEmpty(assemblyVersionString) || !Version.TryParse(assemblyVersionString, out assemblyVersion))
                {
                    string errorMessage = string.Format(CultureInfo.InvariantCulture, Strings.ErrorParsingFrameworkListInvalidValue,
                        frameworkListPath,
                        "Version",
                        assemblyVersionString);
                    log.LogError(errorMessage);
                    yield break;
                }

                yield return new ConflictItem(assemblyName + ".dll",
                                                packageId: null,
                                                assemblyVersion: assemblyVersion,
                                                fileVersion: null);
            }
        }
    }
}
