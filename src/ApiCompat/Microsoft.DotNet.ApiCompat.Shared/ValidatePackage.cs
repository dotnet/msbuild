// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Validators;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class ValidatePackage
    {
        public static void Run(Func<ISuppressionEngine, ICompatibilityLogger> logFactory,
            bool generateSuppressionFile,
            string[]? suppressionFiles,
            string? suppressionOutputFile,
            string? noWarn,
            bool enableRuleAttributesMustMatch,
            string[]? excludeAttributesFiles,
            bool enableRuleCannotChangeParameterName,
            string packagePath,
            bool runApiCompat,
            bool enableStrictModeForCompatibleTfms,
            bool enableStrictModeForCompatibleFrameworksInPackage,
            bool enableStrictModeForBaselineValidation,
            string? baselinePackagePath,
            string? runtimeGraph,
            Dictionary<string, string[]>? packageAssemblyReferences,
            Dictionary<string, string[]>? baselinePackageAssemblyReferences)
        {
            // Initialize the service provider
            ApiCompatServiceProvider serviceProvider = new(logFactory,
                () => new SuppressionEngine(suppressionFiles, noWarn, generateSuppressionFile),
                (log) => new RuleFactory(log,
                    enableRuleAttributesMustMatch,
                    excludeAttributesFiles,
                    enableRuleCannotChangeParameterName));

            // If a runtime graph is provided, parse and use it for asset selection during the in-memory package construction.
            if (runtimeGraph != null)
            {
                Package.InitializeRuntimeGraph(runtimeGraph);
            }

            // Create the in-memory representation of the passed in package path
            Package package = Package.Create(packagePath, packageAssemblyReferences);

            // Invoke all validators and pass the specific validation options in. Don't execute work items, just enqueue them.
            CompatibleTfmValidator tfmValidator = new(serviceProvider.CompatibilityLogger, serviceProvider.ApiCompatRunner);
            tfmValidator.Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleTfms,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            CompatibleFrameworkInPackageValidator compatibleFrameworkInPackageValidator = new(serviceProvider.CompatibilityLogger, serviceProvider.ApiCompatRunner);
            compatibleFrameworkInPackageValidator.Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleFrameworksInPackage,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            if (!string.IsNullOrEmpty(baselinePackagePath))
            {
                BaselinePackageValidator baselineValidator = new(serviceProvider.CompatibilityLogger, serviceProvider.ApiCompatRunner);
                baselineValidator.Validate(new PackageValidatorOption(package,
                    enableStrictMode: enableStrictModeForBaselineValidation,
                    enqueueApiCompatWorkItems: runApiCompat,
                    executeApiCompatWorkItems: false,
                    baselinePackage: Package.Create(baselinePackagePath, baselinePackageAssemblyReferences)));
            }

            if (runApiCompat)
            {
                // Execute the work items that were enqueued.
                serviceProvider.ApiCompatRunner.ExecuteWorkItems();
            }

            if (generateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.SuppressionEngine,
                    serviceProvider.CompatibilityLogger,
                    suppressionFiles,
                    suppressionOutputFile);
            }
        }
    }
}
