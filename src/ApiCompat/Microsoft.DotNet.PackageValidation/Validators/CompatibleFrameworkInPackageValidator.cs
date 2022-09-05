// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Runner;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// Validates that the api surface of the compatible frameworks.
    /// </summary>
    public class CompatibleFrameworkInPackageValidator : IPackageValidator
    {
        private readonly ICompatibilityLogger _log;
        private readonly IApiCompatRunner _apiCompatRunner;

        public CompatibleFrameworkInPackageValidator(ICompatibilityLogger log,
            IApiCompatRunner apiCompatRunner)
        {
            _log = log;
            _apiCompatRunner = apiCompatRunner;
        }

        /// <summary>
        /// Validates that the compatible frameworks have compatible surface area.
        /// </summary>
        /// <param name="options">The options to configure the validator.</param>
        public void Validate(PackageValidatorOption options)
        {
            ApiCompatRunnerOptions apiCompatOptions = new(options.EnableStrictMode);

            ManagedCodeConventions conventions = new(null);
            PatternSet patternSet = options.Package.RefAssets.Any() ?
               conventions.Patterns.CompileRefAssemblies :
               conventions.Patterns.CompileLibAssemblies;

            IEnumerable<ContentItem> compileAssets = options.Package.CompileAssets.OrderByDescending(t => ((NuGetFramework)t.Properties["tfm"]).Version);
            Queue<ContentItem> compileAssetsQueue = new(compileAssets);

            while (compileAssetsQueue.Count > 0)
            {
                ContentItem compileTimeAsset = compileAssetsQueue.Dequeue();
                // If no assets are available for comparison, stop the iteration.
                if (compileAssetsQueue.Count == 0) break;

                // The runtime graph doesn't need to be passed in to the collection as compile time assets can't be rid specific.
                ContentItemCollection contentItemCollection = new();
                // The collection won't contain the current compile time asset as it is already dequeued.
                contentItemCollection.Load(compileAssetsQueue.Select(t => t.Path));
                NuGetFramework framework = (NuGetFramework)compileTimeAsset.Properties["tfm"];
                SelectionCriteria managedCriteria = conventions.Criteria.ForFramework(framework);

                // Searches for a compatible compile time asset and compares it.
                ContentItem? compatibleFrameworkAsset = contentItemCollection.FindBestItemGroup(managedCriteria, patternSet)?.Items.FirstOrDefault();
                if (compatibleFrameworkAsset != null)
                {
                    _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                        compatibleFrameworkAsset,
                        compileTimeAsset,
                        apiCompatOptions,
                        options.Package);
                }
            }

            if (options.ExecuteApiCompatWorkItems)
                _apiCompatRunner.ExecuteWorkItems();
        }
    }
}
