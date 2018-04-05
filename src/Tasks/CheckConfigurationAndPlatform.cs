using Microsoft.Build.Shared;
using System;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Verifies some critical properties like OutputPath are set.  This runs as part of the _CheckForInvalidConfigurationAndPlatform target.
    /// </summary>
    public class CheckConfigurationAndPlatform : TaskExtension
    {
        /// <summary>
        /// Gets or sets the current value of the BaseIntermediateOutputPath property in the project.
        /// </summary>
        public string BaseIntermediateOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the current value of the BuildingInsideVisualStudio property in the project.
        /// </summary>
        public bool BuildingInsideVisualStudio { get; set; }

        /// <summary>
        /// Gets or sets the current value of the IntermediateOutputPath property in the project.
        /// </summary>
        public string IntermediateOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the initial value of the Configuration property in the project.
        /// </summary>
        public string OriginalConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the initial value of the Platform property in the project.
        /// </summary>
        public string OriginalPlatform { get; set; }

        /// <summary>
        /// Gets or sets the current value of the OutDir property in the project.
        /// </summary>
        public string OutDir { get; set; }

        /// <summary>
        /// Gets or sets the current value of the OutputPath property in the project.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Gets or sets the full path to the project that is being evaluated.
        /// </summary>
        public string ProjectFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if an warning should be logged instead of an error when the OutputPath is not set.
        /// </summary>
        public bool SkipInvalidConfigurations { get; set; }

        public override bool Execute()
        {
            if (String.IsNullOrWhiteSpace(OutputPath))
            {
                // A blank OutputPath at this point means that the user passed in an invalid Configuration/Platform combination.
                // Whether this is considered an error or a warning depends on the value of $(SkipInvalidConfigurations).
                if (SkipInvalidConfigurations)
                {
                    Log.LogWarningFromResources(
                        BuildingInsideVisualStudio ? "CheckConfigurationAndPlatformOutputPathInVisualStudio" : "CheckConfigurationAndPlatformOutputPath",
                        ProjectFile,
                        OriginalConfiguration,
                        OriginalPlatform);
                }
                else
                {
                    Log.LogErrorFromResources(
                        BuildingInsideVisualStudio ? "CheckConfigurationAndPlatformOutputPathInVisualStudio" : "CheckConfigurationAndPlatformOutputPath",
                        ProjectFile,
                        OriginalConfiguration,
                        OriginalPlatform);
                }
            }

            if (!String.IsNullOrWhiteSpace(OutDir) && !FileUtilities.EndsWithSlash(OutDir))
            {
                Log.LogErrorFromResources("CheckConfigurationAndPlatformPropertyMustHaveTrailingSlash", "OutDir");
            }

            if (!String.IsNullOrWhiteSpace(BaseIntermediateOutputPath) && !FileUtilities.EndsWithSlash(BaseIntermediateOutputPath))
            {
                Log.LogErrorFromResources("CheckConfigurationAndPlatformPropertyMustHaveTrailingSlash", "BaseIntermediateOutputPath");
            }

            if (!String.IsNullOrWhiteSpace(IntermediateOutputPath) && !FileUtilities.EndsWithSlash(IntermediateOutputPath))
            {
                Log.LogErrorFromResources("CheckConfigurationAndPlatformPropertyMustHaveTrailingSlash", "IntermediateOutputPath");
            }

            return !Log.HasLoggedErrors;
        }
    }
}
