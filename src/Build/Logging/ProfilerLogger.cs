// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Profiler log listener.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Listens to build evaluation finished events and collects profiling information when available
    /// </summary>
    public sealed class ProfilerLogger : ILogger
    {
        private readonly ConcurrentQueue<ProfilerResult> _profiledResults = new ConcurrentQueue<ProfilerResult>();

        /// <nodoc/>
        public string FileToLog { get; }

        /// <nodoc/>
        public ProfilerLogger(string fileToLog)
        {
            FileToLog = fileToLog;
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
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
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
            _profiledResults.Enqueue(projectFinishedEvent.ProfilerResult.Value);
        }

        /// <summary>
        /// Returns the result of aggregating all profiled projects across a build
        /// </summary>
        /// <remarks>
        /// Not thread safe, this method is expected to be called once per build
        /// </remarks>
        public ProfilerResult GetAggregatedResult()
        {
            var aggregatedResults = new Dictionary<EvaluationLocation, ProfiledLocation>();

            while (!_profiledResults.IsEmpty)
            {
                ProfilerResult profiledResult;
                var result = _profiledResults.TryDequeue(out profiledResult);
                Debug.Assert(result, "Expected a non empty queue, this method is not supposed to be called in a multithreaded way");

                foreach (var pair in profiledResult.ProfiledLocations)
                {
                    //  Add elapsed times to evaluation counter dictionaries
                    ProfiledLocation previousTimeSpent;
                    if (!aggregatedResults.TryGetValue(pair.Key, out previousTimeSpent))
                    {
                        previousTimeSpent = new ProfiledLocation(TimeSpan.Zero, TimeSpan.Zero, 0);
                    }

                    var updatedTimeSpent = new ProfiledLocation(
                        previousTimeSpent.InclusiveTime + pair.Value.InclusiveTime,
                        previousTimeSpent.ExclusiveTime + pair.Value.ExclusiveTime,
                        previousTimeSpent.NumberOfHits + 1
                    );

                    aggregatedResults[pair.Key] = updatedTimeSpent;
                }
            }

            return new ProfilerResult(aggregatedResults);
        }
    }
}
