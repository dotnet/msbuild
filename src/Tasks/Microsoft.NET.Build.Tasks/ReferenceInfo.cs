// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class ReferenceInfo
    {
        public string Name { get; }
        public string Version { get; }
        public string FullPath { get; }

        private ReferenceInfo(string name, string version, string fullPath)
        {
            Name = name;
            Version = version;
            FullPath = fullPath;
        }

        public static IEnumerable<ReferenceInfo> CreateFrameworkReferenceInfos(IEnumerable<ITaskItem> referencePaths)
        {
            IEnumerable<ITaskItem> frameworkReferencePaths = referencePaths
                .Where(r => r.GetBooleanMetadata("FrameworkFile") == true ||
                            r.GetMetadata("ResolvedFrom") == "ImplicitlyExpandDesignTimeFacades");

            List<ReferenceInfo> frameworkReferences = new List<ReferenceInfo>();
            foreach (ITaskItem frameworkReferencePath in frameworkReferencePaths)
            {
                string fullPath = frameworkReferencePath.ItemSpec;
                string name = Path.GetFileNameWithoutExtension(fullPath);
                string version = frameworkReferencePath.GetMetadata("Version");

                if (string.IsNullOrEmpty(version))
                {
                    // ImplicitlyExpandDesignTimeFacades adds straight to reference path
                    // without setting version metadata. Use 0.0.0.0 as placeholder.
                    version = "0.0.0.0";
                }

                frameworkReferences.Add(new ReferenceInfo(name, version, fullPath));
            }

            return frameworkReferences;
        }
    }
}
