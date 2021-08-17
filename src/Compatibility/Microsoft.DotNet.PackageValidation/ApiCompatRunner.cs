// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.Compatibility.ErrorSuppression;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Runs ApiCompat over different assembly tuples.
    /// </summary>
    public class ApiCompatRunner
    {
        internal Dictionary<MetadataInformation, List<(MetadataInformation rightAssembly, string header)>> _dict = new();
        private readonly ApiComparer _differ = new();
        private readonly ICompatibilityLogger _log;

        private bool _isBaselineSuppression = false;
        private string _leftPackagePath;
        private string _rightPackagePath;

        public ApiCompatRunner(string noWarn, (string, string)[] ignoredDifferences, bool enableStrictMode, ICompatibilityLogger log)
        {
            _differ.NoWarn = noWarn;
            _differ.IgnoredDifferences = ignoredDifferences;
            _differ.StrictMode = enableStrictMode;
            _log = log;
        }

        /// <summary>
        /// Runs the api compat for the tuples in the dictionary.
        /// </summary>
        public void RunApiCompat()
        {
            foreach (MetadataInformation left in _dict.Keys)
            {
                IAssemblySymbol leftSymbols;
                using(Stream leftAssemblyStream = GetFileStreamFromPackage(_leftPackagePath, left.AssemblyId))
                {
                    leftSymbols = new AssemblySymbolLoader().LoadAssembly(left.AssemblyName, leftAssemblyStream);
                }
                ElementContainer<IAssemblySymbol> leftContainer = new(leftSymbols, left);

                List<ElementContainer<IAssemblySymbol>> rightContainerList = new();
                foreach (var rightTuple in _dict[left])
                {
                    IAssemblySymbol rightSymbols;
                    using (Stream rightAssemblyStream = GetFileStreamFromPackage(_rightPackagePath, rightTuple.rightAssembly.AssemblyId))
                    {
                        rightSymbols = new AssemblySymbolLoader().LoadAssembly(rightTuple.rightAssembly.AssemblyName, rightAssemblyStream);
                    }
                    rightContainerList.Add(new ElementContainer<IAssemblySymbol>(rightSymbols, rightTuple.rightAssembly));
                }

                IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences =
                    _differ.GetDifferences(leftContainer, rightContainerList);

                int counter = 0;
                foreach ((MetadataInformation, MetadataInformation, IEnumerable<CompatDifference> differences) diff in differences)
                {
                    (MetadataInformation rightAssembly, string header) rightTuple = _dict[left][counter++];
                    _log.LogMessage(MessageImportance.Low, rightTuple.header);

                    foreach (CompatDifference difference in diff.differences)
                    {
                        _log.LogError(
                            new Suppression
                            {
                                DiagnosticId = difference.DiagnosticId,
                                Target = difference.ReferenceId,
                                Left = left.AssemblyId,
                                Right = rightTuple.rightAssembly.AssemblyId,
                                IsBaselineSuppression = _isBaselineSuppression
                            },
                            difference.DiagnosticId,
                            difference.Message);
                    }
                }
            }
            _dict.Clear();
        }

        /// <summary>
        /// Queues the api compat for 2 assemblies.
        /// </summary>
        /// <param name="leftMetadataInfo">Metadata information for left assembly.</param>
        /// <param name="rightMetdataInfo">Metadata information for right assembly.</param>
        /// <param name="header">The header for the api compat diagnostics.</param>
        public void QueueApiCompat(MetadataInformation leftMetadataInfo, MetadataInformation rightMetdataInfo, string header)
        {
            if (_dict.TryGetValue(leftMetadataInfo, out var value))
            {
                if (!value.Contains((rightMetdataInfo, header)))
                    value.Add((rightMetdataInfo, header));
            }
            else
            {
                _dict.Add(leftMetadataInfo, new List<(MetadataInformation rightAssembly, string header)>() { (rightMetdataInfo, header) });
            }
        }

        internal void InitializePaths(string leftPackagePath, string rightPackagePath)
        {
            if (string.IsNullOrEmpty(leftPackagePath))
                throw new ArgumentException(nameof(leftPackagePath));

            if (string.IsNullOrEmpty(rightPackagePath))
                throw new ArgumentException(nameof(rightPackagePath));

            _leftPackagePath = leftPackagePath;
            _rightPackagePath = rightPackagePath;

            if (!_leftPackagePath.Equals(rightPackagePath, StringComparison.InvariantCultureIgnoreCase))
            {
                _isBaselineSuppression = true;
            }
        }

        private static Stream GetFileStreamFromPackage(string packagePath, string entry)
        {
            MemoryStream ms = new MemoryStream();
            using (FileStream stream = File.OpenRead(packagePath))
            {
                var zipFile = new ZipArchive(stream);
                zipFile.GetEntry(entry).Open().CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
            }
            return ms;
        }
    }
}
