// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Listens to build evaluation finished events and collects profiling information when available
    /// </summary>
    public sealed class ProfilerLogger : ILogger
    {
        /// <summary>
        /// Accumulates the result of profiling each project. Computing the aggregated result is deferred till the end of the build
        /// to interfere as less as possible with evaluation times
        /// </summary>
        private readonly ConcurrentQueue<ProfilerResult> _profiledResults = new ConcurrentQueue<ProfilerResult>();

        /// <summary>
        /// Aggregation of all profiled locations. Computed the first time <see cref="GetAggregatedResult"/> is called.
        /// </summary>
        private Dictionary<EvaluationLocation, ProfiledLocation> _aggregatedLocations = null;

        /// <summary>
        /// If null, no file is saved to disk
        /// </summary>
        public string FileToLog { get; }

        /// <nodoc/>
        public ProfilerLogger(string fileToLog)
        {
            FileToLog = fileToLog;
        }

        /// <summary>
        /// Creates a logger for testing purposes that gathers profiling information but doesn't save a file to disk with the report
        /// </summary>
        internal static ProfilerLogger CreateForTesting()
        {
            return new ProfilerLogger(fileToLog: null);
        }

        /// <summary>
        /// Verbosity is ignored by this logger
        /// </summary>
        public LoggerVerbosity Verbosity { get; set; }

        /// <summary>
        /// No specific parameters are used by this logger
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Subscribes to status events, which is the category for the evaluation finished event.
        /// </summary>
        public void Initialize(IEventSource eventSource)
        {
            eventSource.StatusEventRaised += ProjectEvaluationFinishedRaised;

            if (eventSource is IEventSource3 eventSource3)
            {
                eventSource3.IncludeEvaluationProfiles();
            }
        }

        /// <summary>
        /// On shutdown, the profiler report is written to disk
        /// </summary>
        public void Shutdown()
        {
            // Tests may pass null so no file is saved to disk
            if (FileToLog != null)
            {
                GenerateProfilerReport();
            }
        }

        private void ProjectEvaluationFinishedRaised(object sender, BuildEventArgs e)
        {
            // We are only interested in project evaluation finished events, and only
            // in the case there is actually a profiler result in it
            var projectFinishedEvent = e as ProjectEvaluationFinishedEventArgs;
            if (projectFinishedEvent?.ProfilerResult == null)
            {
                return;
            }

            // The aggregation is delayed until the whole aggregated result is needed for generating the final content
            // There is a memory/speed trade off here, but we are prioritizing not interfeering with regular evaluation times
            Debug.Assert(_aggregatedLocations == null,
                "GetAggregatedResult was called, but a new ProjectEvaluationFinishedEventArgs arrived after that.");
            _profiledResults.Enqueue(projectFinishedEvent.ProfilerResult.Value);
        }

        /// <summary>
        /// Returns the result of aggregating all profiled projects across a build
        /// </summary>
        /// <param name="pruneSmallItems">Whether small items should be pruned. This is called with false on some tests since the result may vary depending on the evaluator speed</param>
        /// <remarks>
        /// Not thread safe. After this method is called, the assumption is that no new ProjectEvaluationFinishedEventArgs will arrive.
        /// In the regular code path, this method is called only once per build. But some test cases may call it multiple times to validate 
        /// the aggregated data
        /// </remarks>
        internal ProfilerResult GetAggregatedResult(bool pruneSmallItems = true)
        {
            if (_aggregatedLocations != null)
            {
                return new ProfilerResult(_aggregatedLocations);
            }

            // We want to ignore ids so we can appropriately merge entries for the same item
            _aggregatedLocations =
                new Dictionary<EvaluationLocation, ProfiledLocation>(EvaluationLocationIdAgnosticComparer.Singleton);

            // Map from evaluation locations that got merged into the evaluation locations they got merged into
            // This is used to remap the parent id of an incoming item if that parent got merged
            var mergeMap = new Dictionary<long, long>();
            // Unfortunately there is no way to retrieve the original key of a dictionary given a key that is considered Equals
            // So keeping that map here
            var originalLocations = new Dictionary<EvaluationLocation, EvaluationLocation>(EvaluationLocationIdAgnosticComparer.Singleton);

            while (!_profiledResults.IsEmpty)
            {
                ProfilerResult profiledResult;
                var result = _profiledResults.TryDequeue(out profiledResult);
                Debug.Assert(result,
                    "Expected a non empty queue, this method is not supposed to be called in a multithreaded way");

                // Merge all items into the global table.
                // It is important to traverse the profiler locations in ascending order of Ids, so
                // parents are always visited before its children. This guarantees that newly added
                // children always get properly updated regarding merges
                foreach (var pair in profiledResult.ProfiledLocations.OrderBy(p => p.Key.Id))
                {
                    MergeItem(originalLocations, mergeMap, _aggregatedLocations, pair);
                }
            }

            if (pruneSmallItems)
            {
                // After aggregating all items, prune the ones that are too small to report
                _aggregatedLocations = PruneSmallItems(_aggregatedLocations);
            }

            // Add one single top-level item representing the total aggregated evaluation time for globs.
            var aggregatedGlobs = _aggregatedLocations.Keys
                .Where(key => key.Kind == EvaluationLocationKind.Glob)
                .Aggregate(new ProfiledLocation(),
                    (profiledLocation, evaluationLocation) =>
                        AggregateProfiledLocation(profiledLocation, _aggregatedLocations[evaluationLocation]));

            _aggregatedLocations[EvaluationLocation.CreateLocationForAggregatedGlob()] =
                aggregatedGlobs;

            return new ProfilerResult(_aggregatedLocations);
        }

        private static void MergeItem(
            IDictionary<EvaluationLocation, EvaluationLocation> originalLocations,
            IDictionary<long, long> mergeMap,
            IDictionary<EvaluationLocation, ProfiledLocation> aggregatedLocations,
            KeyValuePair<EvaluationLocation, ProfiledLocation> pairToMerge)
        {
            ProfiledLocation existingProfiledLocation;
            if (aggregatedLocations.TryGetValue(pairToMerge.Key, out existingProfiledLocation))
            {
                // A previous item, structurally equivalent, is already there
                // So we aggregate the profiled location times
                var profiledLocation = AggregateProfiledLocation(existingProfiledLocation, pairToMerge.Value);
                // We update the *original* key with the aggregated times, so the location table is kept in a sound state regarding parents
                var originalKey = originalLocations[pairToMerge.Key];
                aggregatedLocations[originalKey] = profiledLocation;
                // And we update the merge map to flag that the merge happened
                mergeMap[pairToMerge.Key.Id] = originalKey.Id;
            }
            else
            {
                // The item is new, so the profiled times are legit, nothing to aggregate
                // But we have to check if this item points to a parent that got merged
                long mergedParent;
                if (pairToMerge.Key.ParentId.HasValue && mergeMap.TryGetValue(pairToMerge.Key.ParentId.Value, out mergedParent))
                {
                    // The parent Id got merged, so update it to the merged parent before storing
                    aggregatedLocations[pairToMerge.Key.WithParentId(mergedParent)] = pairToMerge.Value;
                }
                else
                {
                    // Otherwise, it is safe to add directly
                    aggregatedLocations[pairToMerge.Key] = pairToMerge.Value;
                }
                // Update the original location with the new key, so next time
                // a structurally equivalent key arrives, we can retrieve the original one
                if (!originalLocations.ContainsKey(pairToMerge.Key))
                {
                    originalLocations[pairToMerge.Key] = pairToMerge.Key;
                }
            }
        }

        private static Dictionary<EvaluationLocation, ProfiledLocation> PruneSmallItems(
            IDictionary<EvaluationLocation, ProfiledLocation> aggregatedLocations)
        {
            var result = new Dictionary<EvaluationLocation, ProfiledLocation>();

            // Let's build an index of profiled locations by id, to speed up subsequent queries
            var idTable = aggregatedLocations.ToDictionary(pair => pair.Key.Id,
                pair => new Pair<EvaluationLocation, ProfiledLocation>(pair.Key, pair.Value));

            // We want to keep all evaluation pass entries plus the big enough regular entries
            foreach (var prunedPair in aggregatedLocations.Where(pair =>
                pair.Key.IsEvaluationPass || !IsTooSmall(pair.Value)))
            {
                var key = prunedPair.Key;
                // We already know this pruned pair is something we want to keep. But the parent may be broken since we may remove it
                var parentId = FindBigEnoughParentId(idTable, key.ParentId);
                result[key.WithParentId(parentId)] = prunedPair.Value;
            }

            return result;
        }

        /// <summary>
        /// Finds the first ancestor of parentId (which could be itself) that is either an evaluation pass location or a big enough profiled data
        /// </summary>
        private static long? FindBigEnoughParentId(IDictionary<long, Pair<EvaluationLocation, ProfiledLocation>> idTable,
            long? parentId)
        {
            // The parent id is null, which means the item was pointing to an evaluation pass item. So we keep it as is.
            if (!parentId.HasValue)
            {
                return null;
            }

            var pair = idTable[parentId.Value];

            // We go up the parent relationship until we find an item that is an evaluation pass and is big enough
            while (!pair.Key.IsEvaluationPass && IsTooSmall(pair.Value))
            {
                Debug.Assert(pair.Key.ParentId.HasValue,
                    "A location that is not an evaluation pass should always have a parent");
                pair = idTable[pair.Key.ParentId.Value];
            }

            return pair.Key.Id;
        }

        private static bool IsTooSmall(ProfiledLocation profiledData)
        {
            return profiledData.InclusiveTime.TotalMilliseconds < 1 ||
                   profiledData.ExclusiveTime.TotalMilliseconds < 1;
        }

        private static ProfiledLocation AggregateProfiledLocation(ProfiledLocation location,
            ProfiledLocation otherLocation)
        {
            return new ProfiledLocation(
                location.InclusiveTime + otherLocation.InclusiveTime,
                location.ExclusiveTime + otherLocation.ExclusiveTime,
                location.NumberOfHits + 1
            );
        }

        /// <summary>
        /// Pretty prints the aggregated results and saves it to disk
        /// </summary>
        /// <remarks>
        /// If the extension of the file to log is 'md', markdown content is generated. Otherwise, it falls 
        /// back to a tab separated format
        /// </remarks>
        private void GenerateProfilerReport()
        {
            try
            {
                Console.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("WritingProfilerReport", FileToLog));

                // If the extension of the file is 'md', markdown content is produced. For any other case,
                // a tab separated format is generated
                var content = System.IO.Path.GetExtension(FileToLog) == ".md"
                    ? ProfilerResultPrettyPrinter.GetMarkdownContent(GetAggregatedResult())
                    : ProfilerResultPrettyPrinter.GetTsvContent(GetAggregatedResult());

                File.WriteAllText(FileToLog, content);

                Console.WriteLine(ResourceUtilities.GetResourceString("WritingProfilerReportDone"));
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ErrorWritingProfilerReport", ex.Message));
            }
            catch (IOException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ErrorWritingProfilerReport", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ErrorWritingProfilerReport", ex.Message));
            }
            catch (SecurityException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ErrorWritingProfilerReport", ex.Message));
            }
        }
    }
}
