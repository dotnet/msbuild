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

        //  This task runs both before restore and build, so if it logged warnings directly they could show up
        //  twice when building with implicit restore.  So instead we generate the warnings here, and keep them
        //  in an item where they'll be logged in a target that runs before build, but not before restore.
        [Output]
        public string[] SdkBuildWarnings { get; set; }

        protected override void ExecuteCore()
        {
            List<string> buildWarnings = new List<string>();

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

                        packageReference.SetMetadata(MetadataKeys.IsImplicitlyDefined, "true");
                        packageReference.SetMetadata("PrivateAssets", "all");
                        packageReference.SetMetadata("Publish", "true");

                        packageReferencesToUpdate.Add(packageReference);

                    }
                    else
                    {
                        // NETSDK1071: A PackageReference to '{0}' specified a Version of `{1}`. Specifying the version of this package is not recommended.  For more information, see https://aka.ms/sdkimplicitrefs
                        buildWarnings.Add(string.Format(Strings.PackageReferenceVersionNotRecommended, packageReference.ItemSpec, versionOnPackageReference));
                    }
                }
            }

            PackageReferencesToUpdate = packageReferencesToUpdate.ToArray();
            SdkBuildWarnings = buildWarnings.ToArray();
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
