// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using Microsoft.NET.Build.Tasks;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    static class PlatformManifestReader
    {
        static readonly char[] s_manifestLineSeparator = new[] { '|' };
        public static IEnumerable<ConflictItem> LoadConflictItems(string manifestPath, Logger log)
        {
            if (manifestPath == null)
            {
                throw new ArgumentNullException(nameof(manifestPath));
            }

            if (!File.Exists(manifestPath))
            {
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.CouldNotLoadPlatformManifest,
                    manifestPath);
                log.LogError(errorMessage);
                yield break;
            }

            using (var manifestStream = File.Open(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            using (var manifestReader = new StreamReader(manifestStream))
            {
                for (int lineNumber = 0; !manifestReader.EndOfStream; lineNumber++)
                {
                    var line = manifestReader.ReadLine().Trim();

                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    var lineParts = line.Split(s_manifestLineSeparator);

                    if (lineParts.Length != 4)
                    {
                        string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingPlatformManifest,
                            manifestPath,
                            lineNumber,
                            "fileName|packageId|assemblyVersion|fileVersion");
                        log.LogError(errorMessage);
                        yield break;
                    }

                    var fileName = lineParts[0].Trim();
                    var packageId = lineParts[1].Trim();
                    var assemblyVersionString = lineParts[2].Trim();
                    var fileVersionString = lineParts[3].Trim();

                    Version assemblyVersion = null, fileVersion = null;

                    if (assemblyVersionString.Length != 0 && !Version.TryParse(assemblyVersionString, out assemblyVersion))
                    {
                        string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingPlatformManifestInvalidValue,
                            manifestPath,
                            lineNumber,
                            "AssemblyVersion",
                            assemblyVersionString);
                        log.LogError(errorMessage);
                    }

                    if (fileVersionString.Length != 0 && !Version.TryParse(fileVersionString, out fileVersion))
                    {
                        string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingPlatformManifestInvalidValue,
                            manifestPath,
                            lineNumber,
                            "FileVersion",
                            fileVersionString);
                        log.LogError(errorMessage);
                    }

                    yield return new ConflictItem(fileName, packageId, assemblyVersion, fileVersion);
                }
            }
        }
    }
}
