// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class GetEmbeddedApphostPaths : TaskBase
    {
        /// <summary>
        /// The command name of the dotnet tool.
        /// </summary>
        [Required]
        public string ToolCommandName { get; set; }

        [Required]
        public string PackagedShimOutputDirectory { get; set; }

        /// <summary>
        /// The RuntimeIdentifiers that shims will be generated for.
        /// </summary>
        [Required]
        public ITaskItem[] ShimRuntimeIdentifiers { get; set; }

        /// <summary>
        /// Path of generated shims. metadata "ShimRuntimeIdentifier" is used to map back to input ShimRuntimeIdentifiers.
        /// </summary>
        [Output]
        public ITaskItem[] EmbeddedApphostPaths { get; private set; }

        protected override void ExecuteCore()
        {
            var embeddedApphostPaths = new List<ITaskItem>();
            foreach (var runtimeIdentifier in ShimRuntimeIdentifiers.Select(r => r.ItemSpec))
            {
                var packagedShimOutputDirectoryAndRid = Path.Combine(
                        PackagedShimOutputDirectory,
                        runtimeIdentifier);

                var appHostDestinationFilePath = Path.Combine(
                        packagedShimOutputDirectoryAndRid,
                        ToolCommandName + ExecutableExtension.ForRuntimeIdentifier(runtimeIdentifier));

                var item = new TaskItem(appHostDestinationFilePath);
                item.SetMetadata(MetadataKeys.ShimRuntimeIdentifier, runtimeIdentifier);
                embeddedApphostPaths.Add(item);
            }

            EmbeddedApphostPaths = embeddedApphostPaths.ToArray();
        }
    }
}
