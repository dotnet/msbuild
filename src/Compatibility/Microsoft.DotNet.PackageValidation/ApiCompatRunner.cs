// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        private List<(string leftAssemblyPackagePath, string leftAssemblyRelativePath, string rightAssemblyPackagePath, string rightAssemblyRelativePath, string assemblyName, string compatibilityReason, string header)> _queue = new();
        private readonly ApiComparer _differ = new();
        private readonly IPackageLogger _log;

        public ApiCompatRunner(string noWarn, (string, string)[] ignoredDifferences, IPackageLogger log)
        {
            _differ.NoWarn = noWarn;
            _differ.IgnoredDifferences = ignoredDifferences;
            _log = log;
        }

        /// <summary>
        /// Runs the api compat for the tuples in the queue.
        /// </summary>
        public void RunApiCompat()
        {
            foreach (var apicompatTuples in _queue.Distinct())
            {
                // TODO: Add optimisations tuples.
                using (Stream leftAssemblyStream = GetFileStreamFromPackage(apicompatTuples.leftAssemblyPackagePath, apicompatTuples.leftAssemblyRelativePath))
                using (Stream rightAssemblyStream = GetFileStreamFromPackage(apicompatTuples.rightAssemblyPackagePath, apicompatTuples.rightAssemblyRelativePath))
                {
                    IAssemblySymbol leftSymbols = new AssemblySymbolLoader().LoadAssembly(apicompatTuples.assemblyName, leftAssemblyStream);
                    IAssemblySymbol rightSymbols = new AssemblySymbolLoader().LoadAssembly(apicompatTuples.assemblyName, rightAssemblyStream);

                    _log.LogMessage(MessageImportance.Low, apicompatTuples.header);

                    string leftName = apicompatTuples.leftAssemblyRelativePath;
                    bool isBaselineSuppression = false;
                    if (!apicompatTuples.leftAssemblyPackagePath.Equals(apicompatTuples.rightAssemblyPackagePath, System.StringComparison.InvariantCultureIgnoreCase))
                    {
                        isBaselineSuppression = true;
                        leftName = Resources.Baseline + " " + leftName;
                    }

                    IEnumerable<CompatDifference> differences = _differ.GetDifferences(leftSymbols, rightSymbols, leftName: leftName, rightName: apicompatTuples.rightAssemblyRelativePath);

                    foreach (CompatDifference difference in differences)
                    {
                        _log.LogError(
                            new Suppression
                            {
                                DiagnosticId = difference.DiagnosticId,
                                Target = difference.ReferenceId,
                                Left = apicompatTuples.leftAssemblyRelativePath,
                                Right = apicompatTuples.rightAssemblyRelativePath,
                                IsBaselineSuppression = isBaselineSuppression
                            },
                            difference.DiagnosticId,
                            difference.Message);
                    }
                }
            }
            _queue.Clear();
        }

        /// <summary>
        /// Queues the api compat for 2 assemblies.
        /// </summary>
        /// <param name="leftPackagePath">Path to package containing left assembly.</param>
        /// <param name="leftRelativePath">Relative left assembly path in package.</param>
        /// <param name="rightPackagePath">Path to package containing right assembly.</param>
        /// <param name="rightRelativePath">Relative right assembly path in package.</param>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <param name="compatibilityReason">The reason for assembly compatibilty.</param>
        /// <param name="header">The header for the api compat diagnostics.</param>
        public void QueueApiCompat(string leftPackagePath, string leftRelativePath, string rightPackagePath, string rightRelativePath, string assemblyName, string compatibilityReason, string header)
        {
            _queue.Add((leftPackagePath, leftRelativePath, rightPackagePath, rightRelativePath, assemblyName, compatibilityReason, header));
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
