// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.ApiCompat.Task
{
    /// <summary>
    /// ApiCompat's ValidateAssemblies MSBuild frontend.
    /// </summary>
    public class ValidateAssembliesTask : TaskBase
    {
        // Important: Keep properties exposed in sync with the CLI frontend.

        /// <summary>
        /// The assemblies that represent the contract.
        /// </summary>
        [Required]
        public string[]? LeftAssemblies { get; set; }

        /// <summary>
        /// The assemblies that act as the implementation.
        /// </summary>
        [Required]
        public string[]? RightAssemblies { get; set; }

        /// <summary>
        /// The path to the roslyn assemblies that should be loaded.
        /// </summary>
        [Required]
        public string? RoslynAssembliesPath { get; set; }

        /// <summary>
        /// If true, generates a suppression file that contains the api compatibility errors.
        /// </summary>
        public bool GenerateSuppressionFile { get; set; }

        /// <summary>
        /// If true, preserves unnecessary suppressions when re-generating the suppression file.
        /// </summary>
        public bool PreserveUnnecessarySuppressions { get; set; }

        /// <summary>
        /// If true, permits unnecessary suppressions in the suppression file.
        /// </summary>
        public bool PermitUnnecessarySuppressions { get; set; }

        /// <summary>
        /// The path to suppression files. If provided, the suppressions are read and stored.
        /// </summary>
        public string[]? SuppressionFiles { get; set; }

        /// <summary>
        /// The path to the suppression output file that is written to, when <see cref="GenerateSuppressionFile"/> is true.
        /// </summary>
        public string? SuppressionOutputFile { get; set; }

        /// <summary>
        /// A NoWarn string contains the error codes that should be ignored.
        /// </summary>
        public string? NoWarn { get; set; }

        /// <summary>
        /// If true, includes both internal and public API.
        /// </summary>
        public bool RespectInternals { get; set; }

        /// <summary>
        /// Enables rule to check that attributes match.
        /// </summary>
        public bool EnableRuleAttributesMustMatch { get; set; }

        /// <summary>
        /// Set of files with types in DocId format of which attributes to exclude.
        /// </summary>
        public string[]? ExcludeAttributesFiles { get; set; }

        /// <summary>
        /// Enables rule to check that the parameter names between public methods do not change.
        /// </summary>
        public bool EnableRuleCannotChangeParameterName { get; set; }

        /// <summary>
        /// Performs api comparison checks in strict mode.
        /// </summary>
        public bool EnableStrictMode { get; set; }

        /// <summary>
        /// The left assemblies' references. The index in the array maps to the index of the passed in left assembly.
        /// </summary>
        public string[]? LeftAssembliesReferences { get; set; }

        /// <summary>
        /// The right assemblies' references. The index in the array maps to the index of the passed in right assembly.
        /// </summary>
        public string[]? RightAssembliesReferences { get; set; }

        /// <summary>
        /// The path to a semaphore file that acts as the touched output which is useful for incremental invocation of this task.
        /// </summary>
        public string? SemaphoreFile { get; set; }

        /// <summary>
        /// Create dedicated api compatibility checks for each left and right assembly tuples.
        /// </summary>
        public bool CreateWorkItemPerAssembly { get; set; }

        /// <summary>
        /// Regex transformation patterns (regex + replacement string) that transform left assemblies paths to (i.e. relative) paths that can be encoded into the suppression file.
        /// </summary>
        public ITaskItem[]? LeftAssembliesTransformationPattern { get; set; }

        /// <summary>
        /// Regex transformation patterns (regex + replacement string) that transform right assemblies paths to (i.e. relative) paths that can be encoded into the suppression file.
        /// </summary>
        public ITaskItem[]? RightAssembliesTransformationPattern { get; set; }

        public override bool Execute()
        {
            RoslynResolver roslynResolver = RoslynResolver.Register(RoslynAssembliesPath!);
            try
            {
                return base.Execute();
            }
            finally
            {
                roslynResolver.Unregister();
            }
        }

        protected override void ExecuteCore()
        {
            Func<ISuppressionEngine, SuppressableMSBuildLog> logFactory = (suppressionEngine) => new(Log, suppressionEngine);
            ValidateAssemblies.Run(logFactory,
                GenerateSuppressionFile,
                PreserveUnnecessarySuppressions,
                PermitUnnecessarySuppressions,
                SuppressionFiles,
                SuppressionOutputFile,
                NoWarn,
                RespectInternals,
                EnableRuleAttributesMustMatch,
                ExcludeAttributesFiles,
                EnableRuleCannotChangeParameterName,
                LeftAssemblies!,
                RightAssemblies!,
                EnableStrictMode,
                ParseAssembliesReferences(LeftAssembliesReferences),
                ParseAssembliesReferences(RightAssembliesReferences),
                CreateWorkItemPerAssembly,
                ParseTransformationPattern(LeftAssembliesTransformationPattern),
                ParseTransformationPattern(RightAssembliesTransformationPattern));

            if (SemaphoreFile != null)
            {
                if (Log.HasLoggedErrors)
                {
                    // To force incremental builds to show failures again, delete the passed in semaphore file.
                    if (File.Exists(SemaphoreFile))
                        File.Delete(SemaphoreFile);
                }
                else
                {
                    // If ValidateAssemblies was successful, create/update the semaphore file.
                    File.Create(SemaphoreFile).Dispose();
                    File.SetLastWriteTimeUtc(SemaphoreFile, DateTime.UtcNow);
                }
            }
        }

        private static string[][]? ParseAssembliesReferences(string[]? assembliesReferences)
        {
            if (assembliesReferences == null || assembliesReferences.Length == 0)
                return null;

            string[][] assembliesReferencesArray = new string[assembliesReferences.Length][];
            for (int i = 0; i < assembliesReferences.Length; i++)
            {
                assembliesReferencesArray[i] = assembliesReferences[i].Split(',');
            }

            return assembliesReferencesArray;
        }

        private static (string CaptureGroupPattern, string ReplacementString)[]? ParseTransformationPattern(ITaskItem[]? transformationPatterns)
        {
            const string ReplacementStringMetadataName = "ReplacementString";

            if (transformationPatterns == null)
                return null;

            var patterns = new (string CaptureGroupPattern, string ReplacementPattern)[transformationPatterns.Length];
            for (int i = 0; i < transformationPatterns.Length; i++)
            {
                string captureGroupPattern = transformationPatterns[i].ItemSpec;
                string replacementString = transformationPatterns[i].GetMetadata(ReplacementStringMetadataName);

                if (string.IsNullOrWhiteSpace(replacementString))
                {
                    throw new ArgumentException(string.Format(CommonResources.InvalidRexegStringTransformationPattern,
                        captureGroupPattern,
                        replacementString));
                }

                patterns[i] = (captureGroupPattern, replacementString);
            }

            return patterns;
        }
    }
}
