// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Runs ApiCompat over different assembly tuples.
    /// </summary>
    public class ApiCompatRunner
    {
        internal Dictionary<MetadataInformation, List<(MetadataInformation rightAssembly, string header)>> _dict = new();
        private readonly ApiComparer _differ;
        private readonly CompatibilityLoggerBase _log;
        private readonly Dictionary<string, HashSet<string>>? _referencePaths;
        private readonly string _leftPackagePath;
        private readonly string _rightPackagePath;
        private readonly bool _isBaselineSuppression;

        public ApiCompatRunner(CompatibilityLoggerBase log,
            bool enableStrictMode,
            Dictionary<string, HashSet<string>>? referencePaths,
            string leftPackagePath,
            string? rightPackagePath = null)
        {
            _differ = new()
            {
                StrictMode = enableStrictMode
            };
            _log = log;
            _referencePaths = referencePaths;

            _leftPackagePath = leftPackagePath;
            _rightPackagePath = rightPackagePath ?? leftPackagePath;
            // If assets from different packages are compared, mark the underlying suppression as baseline.
            _isBaselineSuppression = !_leftPackagePath.Equals(_rightPackagePath, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Runs the api compat for the tuples in the dictionary.
        /// </summary>
        public void RunApiCompat()
        {
            foreach (MetadataInformation left in _dict.Keys)
            {
                IAssemblySymbol leftSymbols;
                bool runWithReferences = false;
                using (Stream leftAssemblyStream = GetFileStreamFromPackage(_leftPackagePath, left.AssemblyId))
                {
                    leftSymbols = GetAssemblySymbolFromStream(leftAssemblyStream, left, out runWithReferences);
                }

                ElementContainer<IAssemblySymbol> leftContainer = new(leftSymbols, left);

                List<ElementContainer<IAssemblySymbol>> rightContainerList = new();
                foreach ((MetadataInformation rightAssembly, string header) in _dict[left])
                {
                    IAssemblySymbol rightSymbols;
                    using (Stream rightAssemblyStream = GetFileStreamFromPackage(_rightPackagePath, rightAssembly.AssemblyId))
                    {
                        rightSymbols = GetAssemblySymbolFromStream(rightAssemblyStream, rightAssembly, out bool resolvedReferences);
                        runWithReferences &= resolvedReferences;
                    }

                    rightContainerList.Add(new ElementContainer<IAssemblySymbol>(rightSymbols, rightAssembly));
                }

                _differ.WarnOnMissingReferences = runWithReferences;
                IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                    _differ.GetDifferences(leftContainer, rightContainerList);

                int counter = 0;
                foreach ((MetadataInformation Left, MetadataInformation Right, IEnumerable<CompatDifference> differences) diff in differences)
                {
                    bool headerLogged = false;
                    foreach (CompatDifference difference in diff.differences)
                    {
                        Suppression suppression = new(difference.DiagnosticId)
                        {
                            Target = difference.ReferenceId,
                            Left = diff.Left.AssemblyId,
                            Right = diff.Right.AssemblyId,
                            IsBaselineSuppression = _isBaselineSuppression
                        };

                        // If the error is suppressed, don't log anything.
                        if (_log.SuppressionEngine.IsErrorSuppressed(suppression))
                            continue;

                        // Log the difference header only if there are non suppressed or baselined errors.
                        if (!headerLogged && !_log.BaselineAllErrors)
                        {
                            headerLogged = true;
                            (MetadataInformation rightAssembly, string header) = _dict[left][counter];
                            _log.LogMessage(MessageImportance.Low, header);
                        }

                        _log.LogError(suppression,
                            difference.DiagnosticId,
                            difference.Message);
                    }

                    counter++;
                }
            }

            _dict.Clear();
        }

        private IAssemblySymbol GetAssemblySymbolFromStream(Stream assemblyStream, MetadataInformation assemblyInformation, out bool resolvedReferences)
        {
            resolvedReferences = false;
            HashSet<string>? referencePathForTFM = null;

            // In order to enable reference support for baseline suppression we need a better way
            // to resolve references for the baseline package. Let's not enable it for now.
            bool shouldResolveReferences = !_isBaselineSuppression &&
                _referencePaths != null &&
                _referencePaths.TryGetValue(assemblyInformation.TargetFramework, out referencePathForTFM);

            AssemblySymbolLoader loader = new(resolveAssemblyReferences: shouldResolveReferences);
            if (shouldResolveReferences)
            {
                resolvedReferences = true;
                loader.AddReferenceSearchDirectories(referencePathForTFM);
            }
            else if (!_isBaselineSuppression && _referencePaths?.Count > 0)
            {
                _log.LogWarning(
                    new Suppression(ApiCompatibility.DiagnosticIds.SearchDirectoriesNotFoundForTfm)
                    {
                        Target = assemblyInformation.DisplayString
                    },
                    ApiCompatibility.DiagnosticIds.SearchDirectoriesNotFoundForTfm,
                    Resources.MissingSearchDirectory,
                    assemblyInformation.TargetFramework,
                    assemblyInformation.DisplayString);
            }

            return loader.LoadAssembly(assemblyInformation.AssemblyName, assemblyStream);
        }

        /// <summary>
        /// Queues the api compat for 2 assemblies.
        /// </summary>
        /// <param name="leftMetadataInfo">Metadata information for left assembly.</param>
        /// <param name="rightMetdataInfo">Metadata information for right assembly.</param>
        /// <param name="header">The header for the api compat diagnostics.</param>
        public void QueueApiCompat(MetadataInformation leftMetadataInfo, MetadataInformation rightMetdataInfo, string header)
        {
            if (_dict.TryGetValue(leftMetadataInfo, out List<(MetadataInformation rightAssembly, string header)>? value))
            {
                if (!value.Contains((rightMetdataInfo, header)))
                    value.Add((rightMetdataInfo, header));
            }
            else
            {
                _dict.Add(leftMetadataInfo, new List<(MetadataInformation rightAssembly, string header)>() { (rightMetdataInfo, header) });
            }
        }

        private static Stream GetFileStreamFromPackage(string packagePath, string entry)
        {
            MemoryStream ms = new();
            using (FileStream stream = File.OpenRead(packagePath))
            {
                var zipFile = new ZipArchive(stream);
                zipFile.GetEntry(entry)?.Open().CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
            }
            return ms;
        }
    }
}
