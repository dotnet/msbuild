// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Jab;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Validators;

namespace Microsoft.DotNet.ApiCompat
{
    [ServiceProvider(RootServices = new[] { typeof(IEnumerable<IRule>) })]
    [Import(typeof(IApiCompatServiceProviderModule))]
    [Singleton(typeof(CompatibleFrameworkInPackageValidator))]
    [Singleton(typeof(CompatibleTfmValidator))]
    [Singleton(typeof(BaselinePackageValidator))]
    internal partial class ValidatePackageServiceProvider : IApiCompatServiceProviderModule
    {
        public Func<ICompatibilityLogger> LogFactory { get; }

        public Func<ISuppressionEngine> SuppressionEngineFactory { get; }

        public Func<IRuleFactory> RuleFactory { get; }

        public ValidatePackageServiceProvider(Func<ISuppressionEngine, ICompatibilityLogger> logFactory,
            Func<ISuppressionEngine> suppressionEngineFactory,
            Func<ICompatibilityLogger, IRuleFactory> ruleFactory)
        {
            // It's important to use GetService<T> here instead of directly invoking the factory
            // to avoid two instances being created when retrieving a singleton.
            LogFactory = () => logFactory(GetService<ISuppressionEngine>());
            SuppressionEngineFactory = suppressionEngineFactory;
            RuleFactory = () => ruleFactory(GetService<ICompatibilityLogger>());
        }
    }

    internal static class ValidatePackage
    {
        public static void Run(Func<ISuppressionEngine, ICompatibilityLogger> logFactory,
            bool generateSuppressionFile,
            string? suppressionFile,
            string? noWarn,
            string[]? excludeAttributesFiles,
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
            // Configure the suppression engine. Ignore the passed in suppression file if it should be generated and doesn't yet exist.
            string? suppressionFileForEngine = generateSuppressionFile && !File.Exists(suppressionFile) ? null : suppressionFile;

            // Initialize the service provider
            ValidatePackageServiceProvider serviceProvider = new(logFactory,
                () => new SuppressionEngine(suppressionFileForEngine, noWarn, generateSuppressionFile),
                (log) => new RuleFactory(log, excludeAttributesFiles));

            // If a runtime graph is provided, parse and use it for asset selection during the in-memory package construction.
            if (runtimeGraph != null)
            {
                Package.InitializeRuntimeGraph(runtimeGraph);
            }

            // Create the in-memory representation of the passed in package path
            Package package = Package.Create(packagePath, packageAssemblyReferences);

            // Invoke all validators and pass the specific validation options in. Don't execute work items, just enqueue them.
            serviceProvider.GetService<CompatibleTfmValidator>().Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleTfms,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            serviceProvider.GetService<CompatibleFrameworkInPackageValidator>().Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleFrameworksInPackage,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            if (!string.IsNullOrEmpty(baselinePackagePath))
            {
                serviceProvider.GetService<BaselinePackageValidator>().Validate(new PackageValidatorOption(package,
                    enableStrictMode: enableStrictModeForBaselineValidation,
                    enqueueApiCompatWorkItems: runApiCompat,
                    executeApiCompatWorkItems: false,
                    baselinePackage: Package.Create(baselinePackagePath, baselinePackageAssemblyReferences)));
            }

            if (runApiCompat)
            {
                // Execute the work items that were enqueued.
                serviceProvider.GetService<IApiCompatRunner>().ExecuteWorkItems();
            }

            if (generateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.GetService<ISuppressionEngine>(),
                    serviceProvider.GetService<ICompatibilityLogger>(),
                    suppressionFile);
            }
        }
    }
}
