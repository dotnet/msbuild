// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class CheckIfPackageReferenceShouldBeFrameworkReference : TaskBase
    {
        public ITaskItem[] PackageReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public string PackageReferenceToReplace { get; set; }

        public string FrameworkReferenceToUse { get; set; }

        [Output]
        public bool ShouldRemovePackageReference { get; set; }

        [Output]
        public bool ShouldAddFrameworkReference { get; set; }

        protected override void ExecuteCore()
        {
            foreach (var packageReference in PackageReferences)
            {
                if (packageReference.ItemSpec.Equals(PackageReferenceToReplace, StringComparison.OrdinalIgnoreCase))
                {
                    ShouldRemovePackageReference = true;
                    if (!FrameworkReferences.Any(fr => fr.ItemSpec.Equals(FrameworkReferenceToUse, StringComparison.OrdinalIgnoreCase)))
                    {
                        ShouldAddFrameworkReference = true;
                    }
                }
            }
        }
    }
}
