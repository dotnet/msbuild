// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Runner;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// Validates the api surface between compatible frameworks.
    /// </summary>
    public class CompatibleFrameworkInPackageValidator : IPackageValidator
    {
        private readonly ISuppressibleLog _log;
        private readonly IApiCompatRunner _apiCompatRunner;

        public CompatibleFrameworkInPackageValidator(ISuppressibleLog log,
            IApiCompatRunner apiCompatRunner)
        {
            _log = log;
            _apiCompatRunner = apiCompatRunner;
        }

        /// <summary>
        /// Validates that the compatible frameworks have compatible surface area.
        /// </summary>
        /// <param name="options"><see cref="PackageValidatorOption"/> to configure the compatible framework in package validation.</param>
        public void Validate(PackageValidatorOption options)
        {
            ApiCompatRunnerOptions apiCompatOptions = new(options.EnableStrictMode);

            // The runtime graph doesn't need to be passed in as compile time assets can't be rid specific.
            ManagedCodeConventions conventions = new(null);
            PatternSet patternSet = options.Package.RefAssets.Any() ?
               conventions.Patterns.CompileRefAssemblies :
               conventions.Patterns.CompileLibAssemblies;

            // If the package doesn't contain at least two frameworks, then there's nothing to compare.
            if (options.Package.FrameworksInPackage.Count < 2)
            {
                return;
            }

            Queue<(NuGetFramework, IReadOnlyList<ContentItem>)> compileAssetsQueue = new();
            foreach (NuGetFramework framework in options.Package.FrameworksInPackage.OrderByDescending(f => f.Version))
            {
                IReadOnlyList<ContentItem>? compileTimeAsset = options.Package.FindBestCompileAssetForFramework(framework);
                if (compileTimeAsset != null)
                    compileAssetsQueue.Enqueue((framework, compileTimeAsset));
            }

            while (compileAssetsQueue.Count > 0)
            {
                (NuGetFramework framework, IReadOnlyList<ContentItem> compileTimeAsset) = compileAssetsQueue.Dequeue();

                // If no assets are available for comparison, stop the iteration.
                if (compileAssetsQueue.Count == 0) break;

                SelectionCriteria managedCriteria = conventions.Criteria.ForFramework(framework);

                ContentItemCollection contentItemCollection = new();
                // The collection won't contain the current compile time asset as it is already dequeued.
                contentItemCollection.Load(compileAssetsQueue.SelectMany(a => a.Item2).Select(a => a.Path));

                // Search for a compatible compile time asset and compare it.
                IList<ContentItem>? compatibleFrameworkAsset = contentItemCollection.FindBestItemGroup(managedCriteria, patternSet)?.Items;
                if (compatibleFrameworkAsset != null)
                {
                    _apiCompatRunner.QueueApiCompatFromContentItem(_log,
                        new ReadOnlyCollection<ContentItem>(compatibleFrameworkAsset),
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
