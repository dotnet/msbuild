// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Aggregates the sdk specified as project items and implicit packages references.
    /// Note: this task is not used temporarily as a hack for RTM to be able to get all
    /// implicit SDKs across all TFMs. In U1 we will switch back to this task when multiple
    /// TFM support is added to Dependencies logic. 
    /// Tracking issue https://github.com/dotnet/roslyn-project-system/issues/587
    /// </summary>
    public class CollectSDKReferencesDesignTime : TaskBase
    {
        [Required]
        public ITaskItem[] SdkReferences { get; set; }

        [Required]
        public ITaskItem[] PackageReferences { get; set; }

        [Required]
        public string DefaultImplicitPackages { get; set; }

        [Output]
        public ITaskItem[] SDKReferencesDesignTime { get; set; }

        private HashSet<string> ImplicitPackageReferences { get; set; }

        protected override void ExecuteCore()
        {
            ImplicitPackageReferences = GetImplicitPackageReferences(DefaultImplicitPackages);

            var sdkDesignTimeList = new List<ITaskItem>(SdkReferences);
            sdkDesignTimeList.AddRange(GetImplicitPackageReferences());

            SDKReferencesDesignTime = sdkDesignTimeList.ToArray();
        }

        internal static HashSet<string> GetImplicitPackageReferences(string defaultImplicitPackages)
        {
            var implicitPackageReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(defaultImplicitPackages))
            {
                return implicitPackageReferences;
            }

            var packageNames = defaultImplicitPackages.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (packageNames.Length == 0)
            {
                return implicitPackageReferences;
            }

            foreach (var packageReference in packageNames)
            {
                implicitPackageReferences.Add(packageReference);
            }

            return implicitPackageReferences;
        }

        private IEnumerable<ITaskItem> GetImplicitPackageReferences()
        {
            var implicitPackages = new List<ITaskItem>();
            foreach (var packageReference in PackageReferences)
            {
                var isImplicitlyDefined = false;
                var isImplicitlyDefinedString = packageReference.GetMetadata(MetadataKeys.IsImplicitlyDefined);
                if (string.IsNullOrEmpty(isImplicitlyDefinedString))
                {
                    isImplicitlyDefined = ImplicitPackageReferences.Contains(packageReference.ItemSpec);
                }
                else
                {
                    bool.TryParse(isImplicitlyDefinedString, out isImplicitlyDefined);
                }

                if (isImplicitlyDefined)
                {
                    var newTaskItem = new TaskItem(packageReference.ItemSpec);
                    newTaskItem.SetMetadata(MetadataKeys.SDKPackageItemSpec, string.Empty);
                    newTaskItem.SetMetadata(MetadataKeys.Name, packageReference.ItemSpec);
                    newTaskItem.SetMetadata(MetadataKeys.IsImplicitlyDefined, "True");
                    newTaskItem.SetMetadata(MetadataKeys.Version, packageReference.GetMetadata(MetadataKeys.Version));

                    implicitPackages.Add(newTaskItem);
                }
            }

            return implicitPackages;
        }
    }
}
