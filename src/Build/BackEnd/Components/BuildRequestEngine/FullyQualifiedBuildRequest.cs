// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class represents a build request as it would be received from an MSBuild callback.  Such requests have
    /// configurations which have not yet been assigned a global configuration ID, and therefore must be resolved
    /// with the build manager before a formal request can be sent.
    /// </summary>
    /// <remarks>
    /// This class is called "Fully Qualified" because it completely and directly specifies all of the configuration information.
    /// A standard Build Request only specifies the configuration id, so to get the configuration requires an additional lookup
    /// in a configuration cache.
    /// </remarks>
    internal class FullyQualifiedBuildRequest
    {
        /// <summary>
        /// Initializes a build request.
        /// </summary>
        /// <param name="config">The configuration to use for the request.</param>
        /// <param name="targets">The set of targets to build.</param>
        /// <param name="resultsNeeded">Whether or not to wait for the results of this request.</param>
        /// <param name="skipStaticGraphIsolationConstraints">Whether to skip the constraints of static graph isolation.</param>
        /// <param name="flags">Flags specified for the build request.</param>
        public FullyQualifiedBuildRequest(
            BuildRequestConfiguration config,
            string[] targets,
            bool resultsNeeded,
            bool skipStaticGraphIsolationConstraints = false,
            BuildRequestDataFlags flags = BuildRequestDataFlags.None
            )
        {
            ErrorUtilities.VerifyThrowArgumentNull(config, nameof(config));
            ErrorUtilities.VerifyThrowArgumentNull(targets, nameof(targets));

            Config = config;
            Targets = targets;
            ResultsNeeded = resultsNeeded;
            SkipStaticGraphIsolationConstraints = skipStaticGraphIsolationConstraints;
            BuildRequestDataFlags = flags;
        }

        /// <summary>
        /// Returns the configuration for this request.
        /// </summary>
        public BuildRequestConfiguration Config { get; }

        /// <summary>
        /// Returns the set of targets to be satisfied for this request.
        /// </summary>
        public string[] Targets { get; }

        /// <summary>
        /// Returns true if this request must wait for its results in order to complete.
        /// </summary>
        public bool ResultsNeeded { get; }

        /// <summary>
        /// The set of flags specified in the BuildRequestData for this request.
        /// </summary>
        public BuildRequestDataFlags BuildRequestDataFlags { get; set; }

        public bool SkipStaticGraphIsolationConstraints { get; }

        /// <summary>
        /// Implementation of the equality operator.
        /// </summary>
        /// <param name="left">The left hand argument</param>
        /// <param name="right">The right hand argument</param>
        /// <returns>True if the objects are equivalent, false otherwise.</returns>
        public static bool operator ==(FullyQualifiedBuildRequest left, FullyQualifiedBuildRequest right)
        {
            if (left is null)
            {
                return right is null;
            }

            return !(right is null) && left.InternalEquals(right);
        }

        /// <summary>
        /// Implementation of the inequality operator.
        /// </summary>
        /// <param name="left">The left-hand argument</param>
        /// <param name="right">The right-hand argument</param>
        /// <returns>True if the objects are not equivalent, false otherwise.</returns>
        public static bool operator !=(FullyQualifiedBuildRequest left, FullyQualifiedBuildRequest right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Returns the hash code for this object.
        /// </summary>
        /// <returns>The hash code</returns>
        public override int GetHashCode()
        {
            return Config.GetHashCode();
        }

        /// <summary>
        /// Determines equivalence between this object and another.
        /// </summary>
        /// <param name="obj">The object to which this one should be compared.</param>
        /// <returns>True if the objects are equivalent, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return GetType() == obj.GetType() && InternalEquals((FullyQualifiedBuildRequest) obj);
        }

        /// <summary>
        /// Determines equivalence with another object of the same type.
        /// </summary>
        /// <param name="other">The other object with which to compare this one.</param>
        /// <returns>True if the objects are equivalent, false otherwise.</returns>
        private bool InternalEquals(FullyQualifiedBuildRequest other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Config != other.Config)
            {
                return false;
            }

            if (ResultsNeeded != other.ResultsNeeded)
            {
                return false;
            }

            if (BuildRequestDataFlags != other.BuildRequestDataFlags)
            {
                return false;
            }

            if (Targets.Length != other.Targets.Length)
            {
                return false;
            }

            for (int i = 0; i < Targets.Length; ++i)
            {
                if (Targets[i] != other.Targets[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
