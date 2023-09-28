// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// Represents the .deps.json file in the shared framework
    /// that the CLI is running against.
    /// </summary>
    internal class FrameworkDependencyFile
    {
        private readonly string _depsFilePath;
        private readonly Lazy<DependencyContext> _dependencyContext;

        private DependencyContext DependencyContext => _dependencyContext.Value;

        public FrameworkDependencyFile()
        {
            _depsFilePath = Muxer.GetDataFromAppDomain("FX_DEPS_FILE");
            _dependencyContext = new Lazy<DependencyContext>(CreateDependencyContext);
        }

        public bool IsRuntimeSupported(string runtimeIdentifier)
        {
            return DependencyContext.RuntimeGraph.Any(g => g.Runtime == runtimeIdentifier);
        }

        public string GetNetStandardLibraryVersion()
        {
            return DependencyContext
                .RuntimeLibraries
                .FirstOrDefault(l => "netstandard.library".Equals(l.Name, StringComparison.OrdinalIgnoreCase))
                ?.Version;
        }

#if NETCOREAPP
        public bool TryGetMostFitRuntimeIdentifier(
            string alternativeCurrentRuntimeIdentifier,
            string[] candidateRuntimeIdentifiers,
            out string mostFitRuntimeIdentifier)
        {
            return TryGetMostFitRuntimeIdentifier(
                RuntimeInformation.RuntimeIdentifier,
                alternativeCurrentRuntimeIdentifier,
                DependencyContext.RuntimeGraph,
                candidateRuntimeIdentifiers,
                out mostFitRuntimeIdentifier);
        }
#endif

        internal static bool TryGetMostFitRuntimeIdentifier(
            string currentRuntimeIdentifier,
            string alternativeCurrentRuntimeIdentifier,
            IReadOnlyList<RuntimeFallbacks> runtimeGraph,
            string[] candidateRuntimeIdentifiers,
            out string mostFitRuntimeIdentifier)
        {
            mostFitRuntimeIdentifier = null;
            RuntimeFallbacks[] runtimeFallbacksCandidates;

            if (!string.IsNullOrEmpty(currentRuntimeIdentifier))
            {
                runtimeFallbacksCandidates =
                    runtimeGraph
                    .Where(g => string.Equals(g.Runtime, currentRuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            else
            {
                runtimeFallbacksCandidates = Array.Empty<RuntimeFallbacks>();
            }

            if (runtimeFallbacksCandidates.Length == 0 && !string.IsNullOrEmpty(alternativeCurrentRuntimeIdentifier))
            {
                runtimeFallbacksCandidates =
                    runtimeGraph
                    .Where(g => string.Equals(g.Runtime, alternativeCurrentRuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            if (runtimeFallbacksCandidates.Length == 0)
            {
                return false;
            }

            RuntimeFallbacks runtimeFallbacks = runtimeFallbacksCandidates[0];

            var runtimeFallbacksIncludesRuntime = new List<string>();
            runtimeFallbacksIncludesRuntime.Add(runtimeFallbacks.Runtime);
            runtimeFallbacksIncludesRuntime.AddRange(runtimeFallbacks.Fallbacks);


            var candidateMap = candidateRuntimeIdentifiers
                .Distinct(comparer: StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x, StringComparer.OrdinalIgnoreCase);

            foreach (var fallback in runtimeFallbacksIncludesRuntime)
            {
                if (candidateMap.TryGetValue(fallback, out string match))
                {
                    mostFitRuntimeIdentifier = match;

                    return true;
                }
            }

            return false;
        }

        private DependencyContext CreateDependencyContext()
        {
            using (Stream depsFileStream = File.OpenRead(_depsFilePath))
            using (DependencyContextJsonReader reader = new())
            {
                return reader.Read(depsFileStream);
            }
        }
    }
}
