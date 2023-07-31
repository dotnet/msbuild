// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class GetDefaultPlatformTargetForNetFramework : TaskBase
    {
        public ITaskItem[] PackageDependencies { get; set; }

        public ITaskItem[] NativeCopyLocalItems { get; set; }

        [Output]
        public string DefaultPlatformTarget { get; private set; }

        private const string X86 = "x86";
        private const string AnyCPU = "AnyCPU";

        protected override void ExecuteCore()
        {
            //  For .NET Framework projects, the SDK will select a default RuntimeIdentifier and PlatformTarget.  If no
            //  native assets are found from NuGet packages, then the PlatformTarget will be reset to AnyCPU.  See the
            //  comments in Microsoft.NET.RuntimeIdentifierInference.targets for details.
            //  
            //  Prior to the .NET Core 3.0 SDK, .NET Framework projects would only have a RuntimeIdentifier graph if the
            //  Microsoft.NETCore.Platforms package was (transitively) referenced.  This meant that native assets would
            //  only be selected if the platforms package was referenced or if the RuntimeIdentifier matched exactly.
            //
            //  Now that the RuntimeIdentifier graph is provided in the SDK, the logic in this task preserves the PlatformTarget
            //  behavior from earlier SDKs, even though with the RuntimeIdentifier graph supplied, there may be native
            //  assets selected where in prior SDKs there would not have been.

            if (NativeCopyLocalItems == null || NativeCopyLocalItems.Length == 0)
            {
                DefaultPlatformTarget = AnyCPU;
                return;
            }

            foreach (var packageDependency in PackageDependencies ?? Enumerable.Empty<ITaskItem>())
            {
                //  If the Platforms package is in the dependencies, then any native assets imply an X86 default PlatformTarget
                if (packageDependency.ItemSpec.Equals("Microsoft.NETCore.Platforms", StringComparison.OrdinalIgnoreCase))
                {
                    DefaultPlatformTarget = X86;
                    return;
                }
            }

            foreach (var nativeItem in NativeCopyLocalItems)
            {
                //  If the Platforms package was not referenced, but there are native assets for the exact RID win7-x86,
                //  then the default PlatformTarget should be x86.
                string pathInPackage = nativeItem.GetMetadata(MetadataKeys.PathInPackage);
                if (pathInPackage.StartsWith("runtimes/win7-x86/", StringComparison.OrdinalIgnoreCase))
                {
                    DefaultPlatformTarget = X86;
                    return;
                }
            }

            //  Otherwise, there would have been no native assets selected on pre-3.0 SDKs, so use AnyCPU as the
            //  default PlatformTarget
            DefaultPlatformTarget = AnyCPU;
        }
    }
}
