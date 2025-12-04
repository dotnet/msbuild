// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#nullable disable

namespace Microsoft.Build.Framework.Profiler
{
    /// <summary>
    /// Result of profiling an evaluation
    /// </summary>
    [Serializable]
    public struct ProfilerResult
    {
        /// <nodoc/>
        public IReadOnlyDictionary<EvaluationLocation, ProfiledLocation> ProfiledLocations { get; }

        /// <nodoc/>
        public ProfilerResult(IDictionary<EvaluationLocation, ProfiledLocation> profiledLocations)
        {
            ProfiledLocations = new ReadOnlyDictionary<EvaluationLocation, ProfiledLocation>(profiledLocations);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (!(obj is ProfilerResult result))
            {
                return false;
            }

            return (ProfiledLocations == result.ProfiledLocations) ||
                   (ProfiledLocations.Count == result.ProfiledLocations.Count &&
                    !ProfiledLocations.Except(result.ProfiledLocations).Any());
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return ProfiledLocations.Keys.Aggregate(0, (acum, location) => acum + location.GetHashCode());
        }
    }

    /// <summary>
    /// Result of timing the evaluation of a given element at a given location
    /// </summary>
    [Serializable]
    public struct ProfiledLocation
    {
        /// <nodoc/>
        public TimeSpan InclusiveTime { get; }

        /// <nodoc/>
        public TimeSpan ExclusiveTime { get; }

        /// <nodoc/>
        public int NumberOfHits { get; }

        /// <nodoc/>
        public ProfiledLocation(TimeSpan inclusiveTime, TimeSpan exclusiveTime, int numberOfHits)
        {
            InclusiveTime = inclusiveTime;
            ExclusiveTime = exclusiveTime;
            NumberOfHits = numberOfHits;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ProfiledLocation location &&
                   InclusiveTime.Equals(location.InclusiveTime) &&
                   ExclusiveTime.Equals(location.ExclusiveTime) &&
                   NumberOfHits == location.NumberOfHits;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hashCode = -2131368567;
            hashCode = (hashCode * -1521134295) + EqualityComparer<TimeSpan>.Default.GetHashCode(InclusiveTime);
            hashCode = (hashCode * -1521134295) + EqualityComparer<TimeSpan>.Default.GetHashCode(ExclusiveTime);
            hashCode = (hashCode * -1521134295) + NumberOfHits.GetHashCode();
            return hashCode;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"[{InclusiveTime} - {ExclusiveTime}]: {NumberOfHits} hits";
        }
    }
}
