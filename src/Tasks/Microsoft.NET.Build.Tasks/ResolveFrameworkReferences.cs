// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveFrameworkReferences : TaskBase
    {
        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ResolvedTargetingPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ResolvedRuntimePacks { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] ResolvedFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            if (FrameworkReferences.Length == 0)
            {
                return;
            }

            var resolvedTargetingPacks = ResolvedTargetingPacks.ToDictionary(tp => tp.ItemSpec, StringComparer.OrdinalIgnoreCase);
            var resolvedRuntimePacks = ResolvedRuntimePacks.ToDictionary(rp => rp.GetMetadata(MetadataKeys.FrameworkName), StringComparer.OrdinalIgnoreCase);

            var resolvedFrameworkReferences = new List<TaskItem>(FrameworkReferences.Length);

            foreach (var frameworkReference in FrameworkReferences)
            {
                ITaskItem targetingPack;
                if (!resolvedTargetingPacks.TryGetValue(frameworkReference.ItemSpec, out targetingPack))
                {
                    //  FrameworkReference didn't resolve to a targeting pack
                    continue;
                }

                TaskItem resolvedFrameworkReference = new(frameworkReference.ItemSpec);
                resolvedFrameworkReference.SetMetadata(MetadataKeys.OriginalItemSpec, frameworkReference.ItemSpec);
                resolvedFrameworkReference.SetMetadata(MetadataKeys.IsImplicitlyDefined, frameworkReference.GetMetadata(MetadataKeys.IsImplicitlyDefined));

                resolvedFrameworkReference.SetMetadata("TargetingPackPath", targetingPack.GetMetadata(MetadataKeys.Path));
                resolvedFrameworkReference.SetMetadata("TargetingPackName", targetingPack.GetMetadata(MetadataKeys.NuGetPackageId));
                resolvedFrameworkReference.SetMetadata("TargetingPackVersion", targetingPack.GetMetadata(MetadataKeys.NuGetPackageVersion));
                resolvedFrameworkReference.SetMetadata("Profile", targetingPack.GetMetadata("Profile"));

                ITaskItem runtimePack;
                if (resolvedRuntimePacks.TryGetValue(frameworkReference.ItemSpec, out runtimePack))
                {
                    resolvedFrameworkReference.SetMetadata("RuntimePackPath", runtimePack.GetMetadata(MetadataKeys.PackageDirectory));
                    resolvedFrameworkReference.SetMetadata("RuntimePackName", runtimePack.GetMetadata(MetadataKeys.NuGetPackageId));
                    resolvedFrameworkReference.SetMetadata("RuntimePackVersion", runtimePack.GetMetadata(MetadataKeys.NuGetPackageVersion));
                }

                resolvedFrameworkReferences.Add(resolvedFrameworkReference);
            }

            ResolvedFrameworkReferences = resolvedFrameworkReferences.ToArray();
        }
    }
}
