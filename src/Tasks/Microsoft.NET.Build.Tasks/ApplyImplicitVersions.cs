// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class ApplyImplicitVersions : TaskBase
    {
        public string TargetFrameworkVersion { get; set; }

        public bool TargetLatestRuntimePatch { get; set; }

        public ITaskItem[] PackageReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ImplicitPackageReferenceVersions { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] PackageReferencesToUpdate { get; private set; }

        //  This task runs both before restore and build, so if it logged warnings directly they could show up
        //  twice when building with implicit restore.  So instead we generate the warnings here, and keep them
        //  in an item where they'll be logged in a target that runs before build, but not before restore.
        [Output]
        public string[] SdkBuildWarnings { get; private set; }

        protected override void ExecuteCore()
        {
            List<string> buildWarnings = new();

            var packageReferencesToUpdate = new List<ITaskItem>();

            var implicitVersionTable = GetApplicableImplicitVersionTable();

            foreach (var packageReference in PackageReferences)
            {
                ImplicitPackageReferenceVersion implicitVersion;
                if (implicitVersionTable.TryGetValue(packageReference.ItemSpec, out implicitVersion))
                {
                    string versionOnPackageReference = packageReference.GetMetadata(MetadataKeys.Version);
                    if (string.IsNullOrEmpty(versionOnPackageReference))
                    {
                        packageReference.SetMetadata(MetadataKeys.Version,
                            TargetLatestRuntimePatch ? implicitVersion.LatestVersion : implicitVersion.DefaultVersion);

                        packageReference.SetMetadata(MetadataKeys.IsImplicitlyDefined, "true");

                        packageReferencesToUpdate.Add(packageReference);
                    }
                    else if (!(packageReference.GetBooleanMetadata(MetadataKeys.AllowExplicitVersion) ?? false))
                    {
                        // NETSDK1071: A PackageReference to '{0}' specified a Version of `{1}`. Specifying the version of this package is not recommended.  For more information, see https://aka.ms/sdkimplicitrefs
                        buildWarnings.Add(string.Format(Strings.PackageReferenceVersionNotRecommended, packageReference.ItemSpec, versionOnPackageReference));
                    }
                }
            }

            PackageReferencesToUpdate = packageReferencesToUpdate.ToArray();
            SdkBuildWarnings = buildWarnings.ToArray();
        }

        private Dictionary<string, ImplicitPackageReferenceVersion> GetApplicableImplicitVersionTable()
        {
            var result = new Dictionary<string, ImplicitPackageReferenceVersion>();
            foreach (var item in ImplicitPackageReferenceVersions)
            {
                var implicitPackageReferenceVersion = new ImplicitPackageReferenceVersion(item);

                if (implicitPackageReferenceVersion.TargetFrameworkVersion == TargetFrameworkVersion)
                {
                    result.Add(implicitPackageReferenceVersion.Name, implicitPackageReferenceVersion);
                }
            }

            return result;
        }

        private sealed class ImplicitPackageReferenceVersion
        {
            private ITaskItem _item;

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
