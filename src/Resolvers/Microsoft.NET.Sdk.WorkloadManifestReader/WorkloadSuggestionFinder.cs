// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    internal class WorkloadSuggestionFinder
    {
        public WorkloadSuggestionFinder(HashSet<WorkloadPackId> installedPacks, HashSet<WorkloadPackId> requestedPacks, IEnumerable<(WorkloadDefinitionId id, HashSet<WorkloadPackId> expandedPacks)> expandedWorkloads)
        {
            var partialSuggestions = new List<WorkloadSuggestionCandidate>();
            var completeSuggestions = new HashSet<WorkloadSuggestionCandidate>();

            foreach (var workload in expandedWorkloads)
            {
                if (workload.expandedPacks.Any(e => requestedPacks.Contains(e)))
                {
                    var unsatisfied = new HashSet<WorkloadPackId>(requestedPacks.Where(p => !workload.expandedPacks.Contains(p)));
                    var suggestion = new WorkloadSuggestionCandidate(new HashSet<WorkloadDefinitionId>() { workload.id }, workload.expandedPacks, unsatisfied);
                    if (suggestion.IsComplete)
                    {
                        completeSuggestions.Add(suggestion);
                    }
                    else
                    {
                        partialSuggestions.Add(suggestion);
                    }
                }
            }

            foreach (var root in partialSuggestions)
            {
                GatherCompleteSuggestions(root, partialSuggestions, completeSuggestions);
            }

            UnsortedSuggestions = completeSuggestions.Select(
                c =>
                {
                    var installedCount = c.Packs.Count(p => installedPacks.Contains(p));
                    var extraPacks = c.Packs.Count - installedCount - requestedPacks.Count;
                    return new WorkloadSuggestion(c.Workloads, extraPacks);
                })
                .ToList();
        }

        /// <summary>
        /// Recursively explores a branching tree from the root, adding branches that would reduce the number of unsatisfied packs, and recording any fully satisfied solutions found.
        /// </summary>
        /// <param name="root">A partial suggestion candidate that is the base for this permutation</param>
        /// <param name="branches">Partial suggestion candidates that can be added to the root to make it more complete</param>
        /// <param name="completeSuggestions">A collection to which to add any discovered complete solutions</param>
        private static void GatherCompleteSuggestions(WorkloadSuggestionCandidate root, List<WorkloadSuggestionCandidate> branches, HashSet<WorkloadSuggestionCandidate> completeSuggestions)
        {
            foreach (var branch in branches)
            {
                //skip branches identical to ones that have already already been taken
                //there's probably a more efficient way to do this but this is easy to reason about
                if (root.Workloads.IsSupersetOf(branch.Workloads))
                {
                    continue;
                }

                //skip branches that don't reduce the number of missing packs
                //the branch may be a more optimal solution, but this will be handled elsewhere in the permutation space where it is treated as a root
                if (!root.UnsatisfiedPacks.Overlaps(branch.Packs))
                {
                    continue;
                }

                //construct the new condidate by combining the root and the branch
                var combinedIds = new HashSet<WorkloadDefinitionId>(root.Workloads);
                combinedIds.UnionWith(branch.Workloads);
                var combinedPacks = new HashSet<WorkloadPackId>(root.Packs);
                combinedPacks.UnionWith(branch.Packs);
                var stillMissing = new HashSet<WorkloadPackId>(root.UnsatisfiedPacks);
                stillMissing.ExceptWith(branch.Packs);
                var candidate = new WorkloadSuggestionCandidate(combinedIds, combinedPacks, stillMissing);

                //if the candidate contains all the requested packs, it's complete. else, recurse to try adding more branches to it.
                if (candidate.IsComplete)
                {
                    completeSuggestions.Add(candidate);
                }
                else
                {
                    GatherCompleteSuggestions(candidate, branches, completeSuggestions);
                }
            }
        }

        public ICollection<WorkloadSuggestion> UnsortedSuggestions { get; }

        /// <summary>
        /// Finds the best value from a list of values according to one or more custom comparators. The comparators are an ordered fallback list - the second comparator is only checked when values are equal according to the first comparator, and so on.
        /// </summary>
        private T FindBest<T>(IEnumerable<T> values, params Comparison<T>[] comparators)
        {
            T best = values.First();

            foreach (T val in values.Skip(1))
            {
                foreach (Comparison<T> c in comparators)
                {
                    var cmp = c(val, best);
                    if (cmp > 0)
                    {
                        best = val;
                        break;
                    }
                    else if (cmp < 0)
                    {
                        break;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Gets the suggestion with the lowest number of extra packs and lowest number of workload IDs.
        /// </summary>
        public WorkloadSuggestion GetBestSuggestion() => FindBest(
                UnsortedSuggestions,
                (x, y) => y.ExtraPacks - x.ExtraPacks,
                (x, y) => y.Workloads.Count - x.Workloads.Count);

        /// <summary>
        /// A partial or complete suggestion for workloads to install, annotated with which requested packs it does not satisfy
        /// </summary>
        class WorkloadSuggestionCandidate : IEquatable<WorkloadSuggestionCandidate>
        {
            public WorkloadSuggestionCandidate(HashSet<WorkloadDefinitionId> id, HashSet<WorkloadPackId> packs, HashSet<WorkloadPackId> unsatisfiedPacks)
            {
                Packs = packs;
                UnsatisfiedPacks = unsatisfiedPacks;
                Workloads = id;
            }

            public HashSet<WorkloadDefinitionId> Workloads { get; }
            public HashSet<WorkloadPackId> Packs { get; }
            public HashSet<WorkloadPackId> UnsatisfiedPacks { get; }
            public bool IsComplete => UnsatisfiedPacks.Count == 0;

            public bool Equals(WorkloadSuggestionCandidate? other) => other != null && Workloads.SetEquals(other.Workloads);

            public override int GetHashCode()
            {
                int hashcode = 0;
                foreach (var id in Workloads)
                {
                    hashcode ^= id.GetHashCode();
                }
                return hashcode;
            }
        }

        /// <summary>
        /// A suggestion of one or more workloads to be installed to satisfy missing packs.
        /// </summary>
        public class WorkloadSuggestion
        {
            public WorkloadSuggestion(HashSet<WorkloadDefinitionId> workloads, int extraPacks)
            {
                Workloads = workloads;
                ExtraPacks = extraPacks;
            }

            /// <summary>
            /// The workload IDs that comprise this suggestion
            /// </summary>
            public HashSet<WorkloadDefinitionId> Workloads { get; internal set; }

            /// <summary>
            /// How many additional (and potentionally unnecessary) packs this suggestion will result in installing
            /// </summary>
            public int ExtraPacks { get; internal set; }
        }
    }
}
