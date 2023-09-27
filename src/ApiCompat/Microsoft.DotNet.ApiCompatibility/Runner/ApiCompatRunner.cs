// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiCompatibility.Runner
{
    /// <summary>
    /// Enqueues work items and performs api compatibility checks on them.
    /// </summary>
    public class ApiCompatRunner : IApiCompatRunner
    {
        private readonly HashSet<ApiCompatRunnerWorkItem> _workItems = new();
        private readonly ISuppressibleLog _log;
        private readonly ISuppressionEngine _suppressionEngine;
        private readonly IApiComparerFactory _apiComparerFactory;
        private readonly IAssemblySymbolLoaderFactory _assemblySymbolLoaderFactory;

        /// <inheritdoc />
        public IReadOnlyCollection<ApiCompatRunnerWorkItem> WorkItems => _workItems;

        public ApiCompatRunner(ISuppressibleLog log,
            ISuppressionEngine suppressionEngine,
            IApiComparerFactory apiComparerFactory,
            IAssemblySymbolLoaderFactory assemblySymbolLoaderFactory)
        {
            _suppressionEngine = suppressionEngine;
            _log = log;
            _apiComparerFactory = apiComparerFactory;
            _assemblySymbolLoaderFactory = assemblySymbolLoaderFactory;
        }

        /// <inheritdoc />
        public void ExecuteWorkItems()
        {
            _log.LogMessage(MessageImportance.Low,
                string.Format(Resources.ApiCompatRunnerExecutingWorkItems,
                    _workItems.Count));

            foreach (ApiCompatRunnerWorkItem workItem in _workItems)
            {
                IReadOnlyList<ElementContainer<IAssemblySymbol>> leftContainerList = CreateAssemblySymbols(workItem.Left, workItem.Options, out bool resolvedExternallyProvidedAssemblyReferences);
                bool runWithReferences = resolvedExternallyProvidedAssemblyReferences;

                List<IEnumerable<ElementContainer<IAssemblySymbol>>> rightContainersList = new(workItem.Right.Count);
                foreach (IReadOnlyList<MetadataInformation> right in workItem.Right)
                {
                    IReadOnlyList<ElementContainer<IAssemblySymbol>> rightContainers = CreateAssemblySymbols(right.ToImmutableArray(), workItem.Options, out resolvedExternallyProvidedAssemblyReferences);
                    rightContainersList.Add(rightContainers);
                    runWithReferences &= resolvedExternallyProvidedAssemblyReferences;
                }

                // There must at least be one left and one right element in the container.
                // If assemblies symbols failed to load and nothing is to compare, skip this work item.
                if (leftContainerList.Count == 0 || rightContainersList.Count == 0)
                    continue;

                // Create and configure the work item specific api comparer
                IApiComparer apiComparer = _apiComparerFactory.Create();
                apiComparer.Settings.StrictMode = workItem.Options.EnableStrictMode;
                apiComparer.Settings.WithReferences = runWithReferences;


                // Invoke the api comparer for the work item and operate on the difference result
                IEnumerable<CompatDifference> differences = apiComparer.GetDifferences(leftContainerList, rightContainersList);
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
                            _log.LogError(string.Format(Resources.ApiCompatibilityHeader,
                                difference.Left.AssemblyId,
                                difference.Right.AssemblyId,
                                workItem.Options.IsBaselineComparison ? difference.Left.FullPath : "left",
                                workItem.Options.IsBaselineComparison ? difference.Right.FullPath : "right"));
                        }

                        _log.LogError(suppression,
                            difference.DiagnosticId,
                            difference.Message);
                    }
                }
            }

            _workItems.Clear();
        }

        private IReadOnlyList<ElementContainer<IAssemblySymbol>> CreateAssemblySymbols(IReadOnlyList<MetadataInformation> metadataInformation,
            ApiCompatRunnerOptions options,
            out bool resolvedExternallyProvidedAssemblyReferences)
        {
            string[] aggregatedReferences = metadataInformation.Where(m => m.References != null).SelectMany(m => m.References!).Distinct().ToArray();
            resolvedExternallyProvidedAssemblyReferences = aggregatedReferences.Length > 0;

            IAssemblySymbolLoader loader = _assemblySymbolLoaderFactory.Create(resolvedExternallyProvidedAssemblyReferences);
            if (resolvedExternallyProvidedAssemblyReferences)
            {
                loader.AddReferenceSearchPaths(aggregatedReferences);
            }

            IReadOnlyList<IAssemblySymbol?>? assemblySymbols = null;
            // TODO: Come up with a better pattern to identify archives.
            string? archivePath = metadataInformation[0].FullPath.EndsWith(".nupkg") ? metadataInformation[0].FullPath : null;
            if (archivePath != null)
            {
                string[] relativePaths = metadataInformation.Select(add => add.AssemblyId).ToArray();
                assemblySymbols = loader.LoadAssembliesFromArchive(archivePath, relativePaths);
            }
            else
            {
                string[] assemblyPaths = metadataInformation.Select(add => add.FullPath).ToArray();
                assemblySymbols = loader.LoadAssemblies(assemblyPaths);
            }

            Debug.Assert(assemblySymbols.Count == metadataInformation.Count);
            List<ElementContainer<IAssemblySymbol>> elementContainerList = new(metadataInformation.Count);
            for (int i = 0; i < metadataInformation.Count; i++)
            {
                IAssemblySymbol? assemblySymbol = assemblySymbols[i];
                if (assemblySymbol == null)
                {
                    _log.LogMessage(MessageImportance.High,
                        string.Format(Resources.AssemblyLoadError,
                            metadataInformation[i].AssemblyId));
                    continue;
                }

                elementContainerList.Add(new ElementContainer<IAssemblySymbol>(assemblySymbol, metadataInformation[i]));
            }

            return elementContainerList;
        }

        /// <inheritdoc />
        public void EnqueueWorkItem(ApiCompatRunnerWorkItem workItem)
        {
            // If the work item (left + options) is already part of the queue, add the new right assembly sets to the work item.
            if (_workItems.TryGetValue(workItem, out ApiCompatRunnerWorkItem actualWorkItem))
            {
                foreach (IReadOnlyList<MetadataInformation> right in workItem.Right)
                {
                    bool exists = false;
                    foreach (IReadOnlyList<MetadataInformation> actualRight in actualWorkItem.Right)
                    {
                        // If the new right is already part of the work item, do nothing.
                        if (actualRight.SequenceEqual(right))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        actualWorkItem.Right.Add(right);
                    }
                }
            }
            else
            {
                _workItems.Add(workItem);
            }
        }
    }
}
