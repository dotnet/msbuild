// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class SetGeneratedAppConfigMetadata : TaskBase
    {
        /// <summary>
        /// Path to the app.config source file.
        /// </summary>
        public ITaskItem AppConfigFile { get; set; }

        /// <summary>
        /// Path to the app.config generated source file.
        /// </summary>
        [Required]
        public string GeneratedAppConfigFile { get; set; }

        /// <summary>
        /// Name of the output application config file: $(TargetFileName).config
        /// </summary>
        [Required]
        public string TargetName { get; set; }

        /// <summary>
        /// Path to an intermediate file where we can write the input app.config plus the generated startup supportedRuntime with metadata
        /// </summary>
        [Output]
        public ITaskItem OutputAppConfigFileWithMetadata { get; set; }

        protected override void ExecuteCore()
        {
            OutputAppConfigFileWithMetadata = new TaskItem(GeneratedAppConfigFile);

            if (AppConfigFile != null)
            {
                AppConfigFile.CopyMetadataTo(OutputAppConfigFileWithMetadata);
            }
            else
            {
                OutputAppConfigFileWithMetadata.SetMetadata(MetadataKeys.TargetPath, TargetName);
            }
        }
    }
}
