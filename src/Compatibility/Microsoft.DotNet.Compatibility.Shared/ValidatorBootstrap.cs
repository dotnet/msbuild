// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Validators;

namespace Microsoft.DotNet.Compatibility
{
    internal static class ValidatorBootstrap
    {
        public static void RunValidators(CompatibilityLoggerBase log,
            string packageTargetPath,
            bool generateCompatibilitySuppressionFile,
            bool runApiCompat,
            bool enableStrictModeForCompatibleTfms,
            bool enableStrictModeForCompatibleFrameworksInPackage,
            bool disablePackageBaselineValidation,
            string? baselinePackageTargetPath,
            string? runtimeGraph,
            Dictionary<string, HashSet<string>>? frameworkReferences)
        {
            Package package = Package.Create(packageTargetPath, runtimeGraph);

            new CompatibleTfmValidator(log).Validate(new(package,
                enableStrictModeForCompatibleTfms,
                runApiCompat,
                frameworkReferences));
            new CompatibleFrameworkInPackageValidator(log).Validate(new(
                package,
                enableStrictModeForCompatibleFrameworksInPackage,
                runApiCompat,
                frameworkReferences));

            if (!disablePackageBaselineValidation && !string.IsNullOrEmpty(baselinePackageTargetPath))
            {
                new BaselinePackageValidator(log).Validate(new(
                    package,
                    enableStrictMode: false,
                    runApiCompat,
                    frameworkReferences,
                    baselinePackage: Package.Create(baselinePackageTargetPath, runtimeGraph)));
            }

            if (generateCompatibilitySuppressionFile)
            {
                log.WriteSuppressionFile();
            }
        }
    }
}
