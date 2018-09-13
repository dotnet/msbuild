using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{

    //  TODO: Provide way to opt out of warning when Version is specified (possibly with the DisableImplicitFrameworkReferences property)
    public class ApplyImplicitVersions : TaskBase
    {
        public string TargetFrameworkVersion { get; set; }

        public bool TargetLatestRuntimePatch { get; set; }

        public ITaskItem[] PackageReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ImplicitPackageReferenceVersions { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] PackageReferencesToUpdate { get; set; }

        protected override void ExecuteCore()
        {
            var packageReferencesToUpdate = new List<ITaskItem>();

            var implicitReferencesForThisFramework = ImplicitPackageReferenceVersions
                .Select(item => new ImplicitPackageReferenceVersion(item))
                .Where(item => item.TargetFrameworkVersion == this.TargetFrameworkVersion)
                .ToDictionary(implicitVersion => implicitVersion.Name);

            foreach (var packageReference in PackageReferences)
            {
                ImplicitPackageReferenceVersion implicitVersion;
                if (implicitReferencesForThisFramework.TryGetValue(packageReference.ItemSpec, out implicitVersion))
                {
                    string versionOnPackageReference = packageReference.GetMetadata(MetadataKeys.Version);
                    if (string.IsNullOrEmpty(versionOnPackageReference))
                    {
                        
                        packageReference.SetMetadata(MetadataKeys.Version, 
                            TargetLatestRuntimePatch ? implicitVersion.LatestVersion : implicitVersion.DefaultVersion);

                        packageReferencesToUpdate.Add(packageReference);

                    }
                    else
                    {
                        // NETSDK1071: A PackageReference to '{0}' specified a Version of `{1}`. Specifying the version of this package is not recommended.  For more information, see https://aka.ms/sdkimplicitrefs
                        Log.LogWarning(Strings.PackageReferenceVersionNotRecommended, packageReference.ItemSpec, versionOnPackageReference);
                    }
                }
            }

            PackageReferencesToUpdate = packageReferencesToUpdate.ToArray();
        }

        class ImplicitPackageReferenceVersion
        {
            ITaskItem _item;
            public ImplicitPackageReferenceVersion(ITaskItem item)
            {
                _item = item;
            }
            //  The name / Package ID
            public string Name => _item.ItemSpec;

            //  The target framework version that this item applies to
            public string TargetFrameworkVersion => _item.GetMetadata("TargetFrameworkVersion");

            public string DefaultVersion => _item.GetMetadata("DefaultVersion");

            public string LatestVersion => _item.GetMetadata("LatestVersion");

        }
    }
}
