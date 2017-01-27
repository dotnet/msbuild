// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Aggregates the sdk specified as project items and implicit packages raferences.
    /// </summary>
    public class CollectSDKReferencesDesignTime : TaskBase
    {
        [Required]
        public ITaskItem[] SdkReferences { get; set; }

        [Required]
        public ITaskItem[] PackageReferences { get; set; }

        [Output]
        public ITaskItem[] SDKReferencesDesignTime { get; set; }

        protected override void ExecuteCore()
        {
            var sdkDesignTimeList = new List<ITaskItem>(SdkReferences);
            sdkDesignTimeList.AddRange(GetImplicitPackageReferences());

            SDKReferencesDesignTime = sdkDesignTimeList.ToArray();
        }

        private IEnumerable<ITaskItem> GetImplicitPackageReferences()
        {
            var implicitPackages = new List<ITaskItem>();
            foreach (var packageReference in PackageReferences)
            {
                var isImplicitlyDefinedString = packageReference.GetMetadata(MetadataKeys.IsImplicitlyDefined);
                if (string.IsNullOrEmpty(isImplicitlyDefinedString))
                {
                    continue;
                }

                bool isImplicitlyDefined;
                if (!Boolean.TryParse(isImplicitlyDefinedString, out isImplicitlyDefined)
                    || !isImplicitlyDefined)
                {
                    continue;
                }

                var newTaskItem = new TaskItem(packageReference.ItemSpec);
                newTaskItem.SetMetadata(MetadataKeys.SDKPackageItemSpec, string.Empty);
                newTaskItem.SetMetadata(MetadataKeys.Name, packageReference.ItemSpec);

                implicitPackages.Add(newTaskItem);
            }

            return implicitPackages;
        }
    }
}