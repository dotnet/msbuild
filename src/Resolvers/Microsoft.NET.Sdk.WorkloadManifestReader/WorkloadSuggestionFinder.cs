// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    internal class WorkloadSuggestionFinder
    {
        public WorkloadSuggestionFinder(HashSet<WorkloadPackId> installedPacks, HashSet<WorkloadPackId> requestedPacks, IEnumerable<(WorkloadId id, HashSet<WorkloadPackId> expandedPacks)> expandedWorkloads)
        {
            FindPartialSuggestionsAndSimpleCompleteSuggestions(
                requestedPacks, expandedWorkloads,
                out List<WorkloadSuggestionCandidate> partialSuggestions,
                out HashSet<WorkloadSuggestionCandidate> completeSuggestions);

            foreach (var suggestion in GatherUniqueCompletePermutedSuggestions(partialSuggestions))
            {
                completeSuggestions.Add(suggestion);
            }

            UnsortedSuggestions = completeSuggestions.Select(
                c =>
                {
                    var installedCount = c.Packs.Count(p => installedPacks.Contains(p));
                    var extraPacks = c.Packs.Count - installedCount - requestedPacks.Count;
                    return new WorkloadSuggestion(c.Workloads, extraPacks);
                })
                .ToList();

            if (UnsortedSuggestions.Count == 0)
            {
                throw new ArgumentException("requestedPacks may only contain packs that exist in expandedWorkloads", "requestedPacks");
            }
        }

        /// <summary>
        /// Serachest the list of expanded workloads for workloads that are "simple" complete suggestions themselves and workloads that could be part of a more complex complete suggestion.
        /// </summary>
        /// <param name="requestedPacks">The packs that a complete suggestion must include</param>
        /// <param name="expandedWorkloads">The full set of workloads, flattened to include the packs in the workloads they extend</param>
        /// <param name="partialSuggestions">Workloads that contain one or more of the required packs and could be combined with another workload to make a complete suggestion</param>
        /// <param name="completeSuggestions">Workloads that contain all the requested packs and therefore are inherently complete suggestions</param>
        internal static void FindPartialSuggestionsAndSimpleCompleteSuggestions(
            HashSet<WorkloadPackId> requestedPacks,
            IEnumerable<(WorkloadId id, HashSet<WorkloadPackId> expandedPacks)> expandedWorkloads,
            out List<WorkloadSuggestionCandidate> partialSuggestions,
            out HashSet<WorkloadSuggestionCandidate> completeSuggestions)
        {
            partialSuggestions = new List<WorkloadSuggestionCandidate>();
            completeSuggestions = new HashSet<WorkloadSuggestionCandidate>();
            foreach (var workload in expandedWorkloads)
            {
                if (workload.expandedPacks.Any(e => requestedPacks.Contains(e)))
                {
                    var unsatisfied = new HashSet<WorkloadPackId>(requestedPacks.Where(p => !workload.expandedPacks.Contains(p)));
                    var suggestion = new WorkloadSuggestionCandidate(new HashSet<WorkloadId>() { workload.id }, workload.expandedPacks, unsatisfied);
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
        }

        /// <summary>
        /// Finds complete suggestions by permutationally combining partial suggestions.
        /// </summary>
        /// <param name="partialSuggestions">List of partial suggestions to permutationally combine</param>
        internal static HashSet<WorkloadSuggestionCandidate> GatherUniqueCompletePermutedSuggestions(List<WorkloadSuggestionCandidate> partialSuggestions)
        {
            var completeSuggestions = new HashSet<WorkloadSuggestionCandidate>();

            foreach (var root in partialSuggestions)
            {
                GatherCompletePermutedSuggestions(root, partialSuggestions, completeSuggestions);
            }

            return FilterRedundantSuggestions(completeSuggestions);
        }

        /// <summary>
        /// Recursively explores a branching tree from the root, adding branches that would reduce the number of unsatisfied packs, and recording any fully satisfied solutions found.
        /// This is intended to only be called by <see cref="GatherUniqueCompletePermutedSuggestions"/>.
        /// </summary>
        /// <remarks>
        /// Some of these solutions may contain redundancies, i.e. workloads that are not necessary for it to be a complete solution. These should be filtered out using
        /// <see cref="FilterRedundantSuggestions"/> before using them.
        /// </remarks>
        /// <param name="root">A partial suggestion candidate that is the base for this permutation</param>
        /// <param name="branches">Partial suggestion candidates that can be added to the root to make it more complete</param>
        /// <param name="completeSuggestions">A collection to which to add any discovered complete solutions</param>
        static void GatherCompletePermutedSuggestions(WorkloadSuggestionCandidate root, List<WorkloadSuggestionCandidate> branches, HashSet<WorkloadSuggestionCandidate> completeSuggestions)
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
                var combinedIds = new HashSet<WorkloadId>(root.Workloads);
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
                    GatherCompletePermutedSuggestions(candidate, branches, completeSuggestions);
                }
            }
        }

        /// <summary>
        /// Returns a new set with redundant suggestions removed from it, i.e. suggestions that are a superset of another of the suggestions.
        /// </summary>
        internal static HashSet<WorkloadSuggestionCandidate> FilterRedundantSuggestions(HashSet<WorkloadSuggestionCandidate> completeSuggestions)
        {
            var filtered = new HashSet<WorkloadSuggestionCandidate>();

            foreach (var suggestion in completeSuggestions)
            {
                bool isSupersetOfAny = false;
                foreach (var other in completeSuggestions)
                {
                    if (suggestion.Workloads.IsProperSupersetOf(other.Workloads))
                    {
                        isSupersetOfAny = true;
                    }
                }
                if (!isSupersetOfAny)
                {
                    filtered.Add(suggestion);
                }
            }

            return filtered;
        }

        public ICollection<WorkloadSuggestion> UnsortedSuggestions { get; }

        /// <summary>
        /// Finds the best value from a list of values according to one or more custom comparators. The comparators are an ordered fallback list - the second comparator is only checked when values are equal according to the first comparator, and so on.
        /// </summary>
        private static T FindBest<T>(IEnumerable<T> values, params Comparison<T>[] comparators)
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

        internal static WorkloadSuggestion GetBestSuggestion(ICollection<WorkloadSuggestion> suggestions) => FindBest(
                suggestions,
                (x, y) => y.ExtraPacks - x.ExtraPacks,
                (x, y) => y.Workloads.Count - x.Workloads.Count);

        /// <summary>
        /// Gets the suggestion with the lowest number of extra packs and lowest number of workload IDs.
        /// </summary>
        public WorkloadSuggestion GetBestSuggestion() => GetBestSuggestion(UnsortedSuggestions);

        /// <summary>
        /// A partial or complete suggestion for workloads to install, annotated with which requested packs it does not satisfy
        /// </summary>
        internal class WorkloadSuggestionCandidate : IEquatable<WorkloadSuggestionCandidate>
        {
            public WorkloadSuggestionCandidate(HashSet<WorkloadId> id, HashSet<WorkloadPackId> packs, HashSet<WorkloadPackId> unsatisfiedPacks)
            {
                Packs = packs;
                UnsatisfiedPacks = unsatisfiedPacks;
                Workloads = id;
            }

            public HashSet<WorkloadId> Workloads { get; }
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
            public WorkloadSuggestion(HashSet<WorkloadId> workloads, int extraPacks)
            {
                Workloads = workloads;
                ExtraPacks = extraPacks;
            }

            /// <summary>
            /// The workload IDs that comprise this suggestion
            /// </summary>
            public HashSet<WorkloadId> Workloads { get; internal set; }

            /// <summary>
            /// How many additional (and potentionally unnecessary) packs this suggestion will result in installing
            /// </summary>
            public int ExtraPacks { get; internal set; }
        }
    }
}
