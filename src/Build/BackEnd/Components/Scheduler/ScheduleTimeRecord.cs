// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Class used to track time accumulated during scheduling.
    /// </summary>
    internal class ScheduleTimeRecord
    {
        /// <summary>
        /// The time the current counter started.
        /// </summary>
        private DateTime _startTimeForCurrentState;

        /// <summary>
        /// The accumulated time for this counter.
        /// </summary>
        private TimeSpan _accumulatedTime;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ScheduleTimeRecord()
        {
            _startTimeForCurrentState = DateTime.MinValue;
            _accumulatedTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Retrieve the accumulated time.
        /// </summary>
        public TimeSpan AccumulatedTime
        {
            get
            {
                ErrorUtilities.VerifyThrow(_startTimeForCurrentState == DateTime.MinValue, "Can't get the accumulated time while the timer is still running.");
                return _accumulatedTime;
            }
        }

        /// <summary>
        /// Start the timer.
        /// </summary>
        public void StartState(DateTime currentTime)
        {
            ErrorUtilities.VerifyThrow(_startTimeForCurrentState == DateTime.MinValue, "Cannot start the counter when it is already running.");
            _startTimeForCurrentState = currentTime;
        }

        /// <summary>
        /// End the timer and update the accumulated time.
        /// </summary>
        public void EndState(DateTime currentTime)
        {
            if (_startTimeForCurrentState != DateTime.MinValue)
            {
                _accumulatedTime += currentTime - _startTimeForCurrentState;
                _startTimeForCurrentState = DateTime.MinValue;
            }
        }
    }
}
