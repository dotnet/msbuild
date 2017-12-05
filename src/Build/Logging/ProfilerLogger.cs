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
using System.IO;
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
            Debug.Assert(_aggregatedLocations == null, "GetAggregatedResult was called, but a new ProjectEvaluationFinishedEventArgs arrived after that.");
            _profiledResults.Enqueue(projectFinishedEvent.ProfilerResult.Value);
        }

        /// <summary>
        /// Returns the result of aggregating all profiled projects across a build
        /// </summary>
        /// <remarks>
        /// Not thread safe. After this method is called, the assumption is that no new ProjectEvaluationFinishedEventArgs will arrive.
        /// In the regular code path, this method is called only once per build. But some test cases may call it multiple times to validate 
        /// the aggregated data
        /// </remarks>
        internal ProfilerResult GetAggregatedResult()
        {
            if (_aggregatedLocations != null)
            {
                return new ProfilerResult(_aggregatedLocations);
            }

            _aggregatedLocations = new Dictionary<EvaluationLocation, ProfiledLocation>();

            while (!_profiledResults.IsEmpty)
            {
                ProfilerResult profiledResult;
                var result = _profiledResults.TryDequeue(out profiledResult);
                Debug.Assert(result, "Expected a non empty queue, this method is not supposed to be called in a multithreaded way");

                foreach (var pair in profiledResult.ProfiledLocations)
                {
                    //  Add elapsed times to evaluation counter dictionaries
                    ProfiledLocation previousTimeSpent;
                    if (!_aggregatedLocations.TryGetValue(pair.Key, out previousTimeSpent))
                    {
                        previousTimeSpent = new ProfiledLocation(TimeSpan.Zero, TimeSpan.Zero, 0);
                    }

                    var updatedTimeSpent = new ProfiledLocation(
                        previousTimeSpent.InclusiveTime + pair.Value.InclusiveTime,
                        previousTimeSpent.ExclusiveTime + pair.Value.ExclusiveTime,
                        previousTimeSpent.NumberOfHits + 1
                    );

                    _aggregatedLocations[pair.Key] = updatedTimeSpent;
                }
            }

            return new ProfilerResult(_aggregatedLocations);
        }

        /// <summary>
        /// Gets the markdown content of the aggregated results and saves it to disk
        /// </summary>
        private void GenerateProfilerReport()
        {
            try
            {
                var profilerFile = FileToLog;
                Console.WriteLine(ResourceUtilities.FormatResourceString("WritingProfilerReport", profilerFile));

                var content = ProfilerResultPrettyPrinter.GetMarkdownContent(GetAggregatedResult());
                File.WriteAllText(profilerFile, content);

                Console.WriteLine(ResourceUtilities.GetResourceString("WritingProfilerReportDone"));
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
            catch (IOException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
            catch (SecurityException ex)
            {
                Console.WriteLine(ResourceUtilities.FormatResourceString("ErrorWritingProfilerReport", ex.Message));
            }
        }
    }
}
