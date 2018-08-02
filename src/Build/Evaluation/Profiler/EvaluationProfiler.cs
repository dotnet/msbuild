// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Tracks an assortment of evaluation elements in a stack-like fashion, keeping inclusive and exclusive times for each of them.
    /// </summary>
    internal sealed class EvaluationProfiler
    {
        private readonly bool _shouldTrackElements;
        private readonly Stack<EvaluationFrame> _evaluationStack = new Stack<EvaluationFrame>();
        private readonly Dictionary<EvaluationLocation, ProfiledLocation> _timeSpent = new Dictionary<EvaluationLocation, ProfiledLocation>();

        private EvaluationLocation CurrentLocation => _evaluationStack.Count == 0 ? EvaluationLocation.EmptyLocation : _evaluationStack.Peek().Location;

        /// <summary>
        /// If <param name="shouldTrackElements"/> is false, then requesting to track a given element has no effect and a null <see cref="IDisposable"/> is returned.
        /// </summary>
        internal EvaluationProfiler(bool shouldTrackElements)
        {
            _shouldTrackElements = shouldTrackElements;
        }

        /// <summary>
        /// Contains each evaluated location with its associated timed entry
        /// </summary>
        public ProfilerResult? ProfiledResult => _shouldTrackElements ? (ProfilerResult?)new ProfilerResult(_timeSpent) : null;

        /// <nodoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable TrackPass(EvaluationPass evaluationPass, string passDescription = null)
        {
            return _shouldTrackElements
                ? new EvaluationFrame(this,
                    CurrentLocation.WithEvaluationPass(evaluationPass, passDescription))
                : null;
        }

        /// <nodoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable TrackFile(string file)
        {
            return _shouldTrackElements ? new EvaluationFrame(this, CurrentLocation.WithFile(file)) : null;
        }

        /// <nodoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable TrackGlob(string rootDirectory, string glob, ISet<string> excludePatterns)
        {
            return _shouldTrackElements
                ? new EvaluationFrame(this,
                    CurrentLocation.WithGlob(
                        $"root: '${rootDirectory}', pattern: '${glob}', excludes: '${string.Join(";", excludePatterns)}'"))
                : null;
        }

        /// <nodoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable TrackElement(ProjectElement element)
        {
            return _shouldTrackElements ? new EvaluationFrame(this, CurrentLocation.WithFileLineAndElement(element.Location.File, element.Location.Line, element)) : null;
        }

        /// <nodoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable TrackCondition(IElementLocation location, string condition)
        {
            return _shouldTrackElements ? new EvaluationFrame(this, CurrentLocation.WithFileLineAndCondition(location.File, location.Line, condition)) : null;
        }

        /// <summary>
        /// Returns true when the evaluation stack is empty.
        /// </summary>
        /// <returns></returns>
        internal bool IsEmpty()
        {
            return _evaluationStack.Count == 0;
        }

        /// <summary>
        /// A frame in the evaluation tracker
        /// </summary>
        /// <remarks>
        /// Each frame keeps track of its inclusive and exclusive times
        /// </remarks>
        private sealed class EvaluationFrame : IDisposable
        {
            private readonly EvaluationProfiler _evaluationProfiler;
            private readonly Stopwatch _inclusiveTime = new Stopwatch();
            private readonly Stopwatch _exclusiveTime = new Stopwatch();

            /// <nodoc/>
            public EvaluationLocation Location { get; }

            /// <summary>
            /// Constructs a new evaluation frame and pushes it to the tracker stack
            /// </summary>
            public EvaluationFrame(EvaluationProfiler evaluationProfiler, EvaluationLocation location)
            {
                _evaluationProfiler = evaluationProfiler;
                Location = location;

                _inclusiveTime.Start();
                _exclusiveTime.Start();

                if (_evaluationProfiler._evaluationStack.Count > 0)
                {
                    _evaluationProfiler._evaluationStack.Peek()._exclusiveTime.Stop();
                }

                _evaluationProfiler._evaluationStack.Push(this);
            }

            /// <summary>
            /// Pops this from the tracker stack and computes inclusive and exclusive times
            /// </summary>
            public void Dispose()
            {
                _inclusiveTime.Stop();
                _exclusiveTime.Stop();

                if (_evaluationProfiler._evaluationStack.Pop() != this)
                {
                    throw new InvalidOperationException("Evaluation frame disposed out of order");
                }

                if (_evaluationProfiler._evaluationStack.Count > 0)
                {
                    _evaluationProfiler._evaluationStack.Peek()._exclusiveTime.Start();
                }

                //  Add elapsed times to evaluation counter dictionaries
                if (!_evaluationProfiler.ProfiledResult.Value.ProfiledLocations.TryGetValue(Location, out var previousTimeSpent))
                {
                    previousTimeSpent = new ProfiledLocation(TimeSpan.Zero, TimeSpan.Zero, 0);
                }
                
                var updatedTimeSpent = new ProfiledLocation(
                        previousTimeSpent.InclusiveTime + _inclusiveTime.Elapsed,
                        previousTimeSpent.ExclusiveTime + _exclusiveTime.Elapsed,
                        0
                    );

                _evaluationProfiler._timeSpent[Location] = updatedTimeSpent;
            }
        }
    }
}
