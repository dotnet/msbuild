// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Compatibility.Console
{
    class Program
    {
        /// <summary>
        /// Microsoft.DotNet.Compatibility.Console
        /// </summary>
        /// <param name="argument">The path to the package to be checked for compatibility</param>
        /// <param name="suppressionFile">The path to the compatibility suppression file</param>
        /// <param name="generateSuppressionFile">If true, generates a compatibility suppression file</param>
        /// <param name="runApiCompat">If true, runs ApiCompat as part of this tool</param>
        /// <param name="enableStrictModeForCompatibleTfms">If true, enables strict mode for the compatible tfm validator</param>
        /// <param name="enableStrictModeForCompatibleFrameworksInPackage">If true, enables strict mode for the compatible frameworks validator</param>
        /// <param name="disablePackageBaselineValidation">If true, disables the package baseline validator</param>
        /// <param name="baselinePackage">The path to the baseline package to compare against</param>
        /// <param name="noWarn">A NoWarn string that allows to disable specific rules</param>
        /// <param name="runtimeGraph">The path to the runtime graph file</param>
        /// <param name="referencePathItems">A string containing tuples of reference assembly paths plus the target framework.</param>
        static void Main(string argument,
            string? suppressionFile = null,
            bool generateSuppressionFile = false,
            bool runApiCompat = true,
            bool enableStrictModeForCompatibleTfms = true,
            bool enableStrictModeForCompatibleFrameworksInPackage = false,
            bool disablePackageBaselineValidation = false,
            string? baselinePackage = null,
            string? noWarn = null,
            string? runtimeGraph = null,
            List<(string tfm, string referencePath)>? referencePathItems = null)
        {
            Dictionary<string, HashSet<string>> frameworkReferences = new();
            if (referencePathItems != null)
            {
                foreach ((string tfm, string referencePath) in referencePathItems)
                {
                    if (string.IsNullOrEmpty(tfm) || !File.Exists(referencePath))
                        continue;

                    if (!frameworkReferences.TryGetValue(tfm, out HashSet<string>? directories))
                    {
                        directories = new();
                        frameworkReferences.Add(tfm, directories);
                    }

                    directories.Add(referencePath);
                }
            }

            ConsoleCompatibilityLogger log = new(suppressionFile, generateSuppressionFile, noWarn);
            ValidatorBootstrap.RunValidators(log,
                argument,
                generateSuppressionFile,
                runApiCompat,
                enableStrictModeForCompatibleTfms,
                enableStrictModeForCompatibleFrameworksInPackage,
                disablePackageBaselineValidation,
                baselinePackage,
                runtimeGraph,
                frameworkReferences);
        }
    }
}
