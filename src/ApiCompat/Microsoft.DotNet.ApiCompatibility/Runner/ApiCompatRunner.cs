// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Runner
{
    /// <summary>
    /// Enqueues work items and performs api compatibility checks on them.
    /// </summary>
    public class ApiCompatRunner : IApiCompatRunner
    {
        private readonly HashSet<ApiCompatRunnerWorkItem> _workItems = new();
        private readonly ICompatibilityLogger _log;
        private readonly ISuppressionEngine _suppressionEngine;
        private readonly IApiComparerFactory _apiComparerFactory;
        private readonly IAssemblySymbolLoaderFactory _assemblySymbolLoaderFactory;
        private readonly IMetadataStreamProvider _metadataStreamProvider;

        /// <inheritdoc />
        public IReadOnlyCollection<ApiCompatRunnerWorkItem> WorkItems => _workItems;

        public ApiCompatRunner(ICompatibilityLogger log,
            ISuppressionEngine suppressionEngine,
            IApiComparerFactory apiComparerFactory,
            IAssemblySymbolLoaderFactory assemblySymbolLoaderFactory,
            IMetadataStreamProvider metadataStreamProvider)
        {
            _suppressionEngine = suppressionEngine;
            _log = log;
            _apiComparerFactory = apiComparerFactory;
            _assemblySymbolLoaderFactory = assemblySymbolLoaderFactory;
            _metadataStreamProvider = metadataStreamProvider;
        }

        /// <inheritdoc />
        public void ExecuteWorkItems()
        {
            _log.LogMessage(MessageImportance.Low, Resources.ApiCompatRunnerExecutingWorkItems, _workItems.Count.ToString());

            foreach (ApiCompatRunnerWorkItem workItem in _workItems)
            {
                bool runWithReferences = true;

                List<ElementContainer<IAssemblySymbol>> leftContainerList = new();
                foreach (MetadataInformation left in workItem.Lefts)
                {
                    IAssemblySymbol? leftAssemblySymbol;
                    using (Stream leftAssemblyStream = _metadataStreamProvider.GetStream(left))
                    {
                        leftAssemblySymbol = GetAssemblySymbolFromStream(leftAssemblyStream, left, workItem.Options, out bool resolvedReferences);
                        runWithReferences &= resolvedReferences;
                    }

                    if (leftAssemblySymbol == null)
                    {
                        _log.LogMessage(MessageImportance.High, string.Format(Resources.AssemblyLoadError, left.AssemblyId));
                        continue;
                    }

                    leftContainerList.Add(new ElementContainer<IAssemblySymbol>(leftAssemblySymbol, left));
                }

                List<ElementContainer<IAssemblySymbol>> rightContainerList = new();
                foreach (MetadataInformation right in workItem.Rights)
                {
                    IAssemblySymbol? rightAssemblySymbol;
                    using (Stream rightAssemblyStream = _metadataStreamProvider.GetStream(right))
                    {
                        rightAssemblySymbol = GetAssemblySymbolFromStream(rightAssemblyStream, right, workItem.Options, out bool resolvedReferences);
                        runWithReferences &= resolvedReferences;
                    }

                    if (rightAssemblySymbol == null)
                    {
                        _log.LogMessage(MessageImportance.High, string.Format(Resources.AssemblyLoadError, right.AssemblyId));
                        continue;
                    }

                    rightContainerList.Add(new ElementContainer<IAssemblySymbol>(rightAssemblySymbol, right));
                }

                // There must at least be one left and one right element in the container.
                // If assemblies symbols failed to load and nothing is to compare, skip this work item.
                if (leftContainerList.Count == 0 || rightContainerList.Count == 0)
                    continue;

                // Create and configure the work item specific api comparer
                IApiComparer apiComparer = _apiComparerFactory.Create(new ApiComparerSettings(
                    strictMode: workItem.Options.EnableStrictMode,
                    withReferences: runWithReferences));

                // Invoke the api comparer for the work item and operate on the difference result
                IEnumerable<CompatDifference> differences = apiComparer.GetDifferences(leftContainerList, rightContainerList);
                var differenceGroups = differences.GroupBy((c) => new { c.Left, c.Right });

                foreach (var differenceGroup in differenceGroups)
                {
                    // Log the difference header only if there are differences and errors aren't baselined.
                    bool logHeader = !_suppressionEngine.BaselineAllErrors;

                    foreach (CompatDifference difference in differenceGroup)
                    {
                        Suppression suppression = new(difference.DiagnosticId)
                        {
                            Target = difference.ReferenceId,
                            Left = difference.Left.AssemblyId,
                            Right = difference.Right.AssemblyId,
                            IsBaselineSuppression = workItem.Options.IsBaselineComparison
                        };

                        // If the error is suppressed, don't log anything.
                        if (_suppressionEngine.IsErrorSuppressed(suppression))
                            continue;

                        if (logHeader)
                        {
                            logHeader = false;
                            _log.LogMessage(MessageImportance.Normal,
                                Resources.ApiCompatibilityHeader,
                                difference.Left.AssemblyId,
                                difference.Right.AssemblyId,
                                workItem.Options.IsBaselineComparison ? difference.Left.FullPath : "left",
                                workItem.Options.IsBaselineComparison ? difference.Right.FullPath : "right");
                        }

                        _log.LogError(suppression,
                            difference.DiagnosticId,
                            difference.Message);
                    }

                    _log.LogMessage(MessageImportance.Low,
                        Resources.ApiCompatibilityFooter,
                        differenceGroup.Key.Left.AssemblyId,
                        differenceGroup.Key.Right.AssemblyId,
                        workItem.Options.IsBaselineComparison ? differenceGroup.Key.Left.FullPath : "left",
                        workItem.Options.IsBaselineComparison ? differenceGroup.Key.Right.FullPath : "right");
                }
            }

            _workItems.Clear();
        }

        private IAssemblySymbol? GetAssemblySymbolFromStream(Stream assemblyStream, MetadataInformation assemblyInformation, ApiCompatRunnerOptions options, out bool resolvedReferences)
        {
            resolvedReferences = false;

            // In order to enable reference support for baseline suppression we need a better way
            // to resolve references for the baseline package. Let's not enable it for now.
            bool shouldResolveReferences = !options.IsBaselineComparison &&
                assemblyInformation.References != null;

            // Create the work item specific assembly symbol loader and configure if references should be resolved
            IAssemblySymbolLoader loader = _assemblySymbolLoaderFactory.Create(shouldResolveReferences);
            if (shouldResolveReferences)
            {
                resolvedReferences = true;
                loader.AddReferenceSearchDirectories(assemblyInformation.References!);
            }

            return loader.LoadAssembly(assemblyInformation.AssemblyName, assemblyStream);
        }

        /// <inheritdoc />
        public void EnqueueWorkItem(ApiCompatRunnerWorkItem workItem)
        {
            if (_workItems.TryGetValue(workItem, out ApiCompatRunnerWorkItem actualWorkItem))
            {
                foreach (MetadataInformation right in workItem.Rights)
                {
                    actualWorkItem.Rights.Add(right);
                }
            }
            else
            {
                _workItems.Add(workItem);
            }
        }
    }
}
