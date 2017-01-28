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
    /// Aggregates the sdk specified as project items and implicit
    /// packages produced by ResolvePackageDependencies.
    /// </summary>
    public class CollectResolvedSDKReferencesDesignTime : TaskBase
    {
        [Required]
        public ITaskItem[] ResolvedSdkReferences { get; set; }

        [Required]
        public ITaskItem[] DependenciesDesignTime { get; set; }

        [Output]
        public ITaskItem[] ResolvedSDKReferencesDesignTime { get; set; }

        protected override void ExecuteCore()
        {
            var sdkDesignTimeList = new List<ITaskItem>(ResolvedSdkReferences);
            sdkDesignTimeList.AddRange(GetTopLevelImplicitPackageDependencies());

            ResolvedSDKReferencesDesignTime = sdkDesignTimeList.ToArray();
        }

        private IEnumerable<ITaskItem> GetTopLevelImplicitPackageDependencies()
        {
            var uniqueTopLevelPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var topLevelPackages = new List<ITaskItem>();
            foreach (var dependencyItem in DependenciesDesignTime)
            {
                var dependencyItemWrapper = new DependencyItemWrapper(dependencyItem);
                if (!(dependencyItemWrapper.Type == DependencyType.Package &&
                      dependencyItemWrapper.IsTopLevelDependency &&
                      dependencyItemWrapper.IsImplicitlyDefined))
                {
                    continue;
                }

                var itemName = dependencyItem.GetMetadata(MetadataKeys.Name);
                if (uniqueTopLevelPackages.Contains(itemName))
                {
                    continue;
                }

                uniqueTopLevelPackages.Add(itemName);

                var newTaskItem = new TaskItem(itemName);
                newTaskItem.SetMetadata(MetadataKeys.SDKPackageItemSpec, dependencyItem.ItemSpec);
                newTaskItem.SetMetadata(MetadataKeys.Name, itemName);
                newTaskItem.SetMetadata(MetadataKeys.Version, dependencyItem.GetMetadata(MetadataKeys.Version));
                newTaskItem.SetMetadata(MetadataKeys.SDKRootFolder, dependencyItem.GetMetadata(MetadataKeys.Path));
                newTaskItem.SetMetadata(MetadataKeys.OriginalItemSpec, itemName);
                
                topLevelPackages.Add(newTaskItem);
            }

            return topLevelPackages;
        }

        private class DependencyItemWrapper
        {
            public DependencyItemWrapper(ITaskItem item)
            {
                var dependencyTypeString = item.GetMetadata(MetadataKeys.Type) ?? string.Empty;
                if (Enum.TryParse(dependencyTypeString ?? string.Empty, /*ignoreCase */ true, out DependencyType dependencyType))
                {
                    Type = dependencyType;
                }
                else
                {
                    Type = DependencyType.Unknown;
                }

                bool.TryParse(item.GetMetadata(MetadataKeys.IsTopLevelDependency) ?? string.Empty, out bool isTopLevelDependency);
                IsTopLevelDependency = isTopLevelDependency;

                bool.TryParse(item.GetMetadata(MetadataKeys.IsImplicitlyDefined) ?? string.Empty, out bool isImplicitlyDefined);
                IsImplicitlyDefined = isImplicitlyDefined;
            }

            public DependencyType Type { get; }
            public bool IsTopLevelDependency { get; }
            public bool IsImplicitlyDefined { get; }
        }
    }
}