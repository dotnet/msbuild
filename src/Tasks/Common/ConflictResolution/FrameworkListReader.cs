// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using Microsoft.NET.Build.Tasks;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using Microsoft.Build.Framework;
using System.Reflection;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    class FrameworkListReader
    {
        private IBuildEngine4 _buildEngine;

        public FrameworkListReader(IBuildEngine4 buildEngine)
        {
            _buildEngine = buildEngine;
        }

        public IEnumerable<ConflictItem> GetConflictItems(string frameworkListPath, Logger log)
        {
            if (frameworkListPath == null)
            {
                throw new ArgumentNullException(nameof(frameworkListPath));
            }

            if (!Path.IsPathRooted(frameworkListPath))
            {
                throw new BuildErrorException(Strings.FrameworkListPathNotRooted, frameworkListPath);
            }


            //  Need to include assembly name in the key here, since both Microsoft.NET.Build.Tasks and Microsoft.NET.Build.Extensions.Tasks share this code,
            //  but can't share the types of the ConflictItem objects.
            string assemblyName = typeof(FrameworkListReader).GetTypeInfo().Assembly.FullName;

            string objectKey = $"{assemblyName}:{nameof(FrameworkListReader)}:{frameworkListPath}";

            IEnumerable<ConflictItem> result;

            object existingConflictItems = _buildEngine.GetRegisteredTaskObject(objectKey, RegisteredTaskObjectLifetime.AppDomain);

            if (existingConflictItems == null)
            {
                result = LoadConflictItems(frameworkListPath, log);

                _buildEngine.RegisterTaskObject(objectKey, result, RegisteredTaskObjectLifetime.AppDomain, true);
            }
            else
            {
                result = (IEnumerable<ConflictItem>)existingConflictItems;
            }

            return result;
        }

        private static IEnumerable<ConflictItem> LoadConflictItems(string frameworkListPath, Logger log)
        {
            if (!File.Exists(frameworkListPath))
            {
                //  This is not an error, as we get both the root target framework directory as well as the Facades folder passed in as TargetFrameworkDirectories.
                //  Only the root will have a RedistList\FrameworkList.xml in it
                return Enumerable.Empty<ConflictItem>();
            }

            var frameworkList = XDocument.Load(frameworkListPath);
            var ret = new List<ConflictItem>();
            foreach (var file in frameworkList.Root.Elements("File"))
            {
                var type = file.Attribute("Type")?.Value;

                if (type?.Equals("Analyzer", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    continue;
                }

                var assemblyName = file.Attribute("AssemblyName")?.Value;
                var assemblyVersionString = file.Attribute("Version")?.Value;

                if (string.IsNullOrEmpty(assemblyName))
                {
                    string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingFrameworkListInvalidValue,
                        frameworkListPath,
                        "AssemblyName",
                        assemblyName);
                    log.LogError(errorMessage);
                    return Enumerable.Empty<ConflictItem>();
                }

                Version assemblyVersion;
                if (string.IsNullOrEmpty(assemblyVersionString) || !Version.TryParse(assemblyVersionString, out assemblyVersion))
                {
                    string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingFrameworkListInvalidValue,
                        frameworkListPath,
                        "Version",
                        assemblyVersionString);
                    log.LogError(errorMessage);
                    return Enumerable.Empty<ConflictItem>();
                }

                ret.Add(new ConflictItem(assemblyName + ".dll",
                                                packageId: "TargetingPack",
                                                assemblyVersion: assemblyVersion,
                                                fileVersion: null));
            }

            return ret;
        }
    }
}
