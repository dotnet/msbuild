// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Utilities;

namespace Microsoft.DotNet.ProjectModel.Resources
{
    public static class ResourceUtility
    {
        public static string GetResourceName(string projectFolder, string resourcePath)
        {
            // If the file is outside of the project folder, we are assuming it is directly in the root
            // otherwise, keep the folders that are inside the project
            return PathUtility.IsChildOfDirectory(projectFolder, resourcePath) ?
                PathUtility.GetRelativePath(projectFolder, resourcePath) :
                Path.GetFileName(resourcePath);
        }

        public static bool IsResourceFile(string fileName)
        {
            var ext = Path.GetExtension(fileName);

            return
                IsResxFile(fileName) ||
                string.Equals(ext, ".restext", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".resources", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsResxFile(string fileName)
        {
            var ext = Path.GetExtension(fileName);

            return string.Equals(ext, ".resx", StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<string> GetResourcesForCulture(string cultureName, IList<string> resources)
        {
            var resourcesByCultureName = resources
                .GroupBy(GetResourceCultureName, StringComparer.OrdinalIgnoreCase);

            if (string.Equals(cultureName, "neutral", StringComparison.OrdinalIgnoreCase))
            {
                cultureName = string.Empty;
            }

            return resourcesByCultureName
                .SingleOrDefault(grouping => string.Equals(grouping.Key, cultureName, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetResourceCultureName(string res)
        {
            var ext = Path.GetExtension(res);

            if (IsResourceFile(res))
            {
                var resourceBaseName = Path.GetFileNameWithoutExtension(Path.GetFileName(res));
                var cultureName = Path.GetExtension(resourceBaseName);
                if (string.IsNullOrEmpty(cultureName) || cultureName.Length < 3)
                {
                    return string.Empty;
                }

                // Path.Extension adds a . to the file extension name; example - ".resources". Removing the "." with Substring
                cultureName = cultureName.Substring(1);

                if (CultureInfoCache.KnownCultureNames.Contains(cultureName))
                {
                    return cultureName;
                }
            }

            return string.Empty;
        }
    }
}