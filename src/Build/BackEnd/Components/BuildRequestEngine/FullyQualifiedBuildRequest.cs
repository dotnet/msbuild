// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Build request which has not had its configuration resolved yet.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
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
        /// The request's configuration.
        /// </summary>
        private BuildRequestConfiguration _requestConfiguration;

        /// <summary>
        /// The set of targets to build.
        /// </summary>
        private string[] _targets;

        /// <summary>
        /// Whether or not we need to wait for results before completing this request.
        /// </summary>
        private bool _resultsNeeded;

        /// <summary>
        /// Initializes a build request.
        /// </summary>
        /// <param name="config">The configuration to use for the request.</param>
        /// <param name="targets">The set of targets to build.</param>
        /// <param name="resultsNeeded">Whether or not to wait for the results of this request.</param>
        public FullyQualifiedBuildRequest(BuildRequestConfiguration config, string[] targets, bool resultsNeeded)
        {
            ErrorUtilities.VerifyThrowArgumentNull(config, "config");
            ErrorUtilities.VerifyThrowArgumentNull(targets, "targets");

            _requestConfiguration = config;
            _targets = targets;
            _resultsNeeded = resultsNeeded;
        }

        /// <summary>
        /// Returns the configuration for this request.
        /// </summary>
        public BuildRequestConfiguration Config
        {
            get
            {
                return _requestConfiguration;
            }
        }

        /// <summary>
        /// Returns the set of targets to be satisfied for this request.
        /// </summary>
        public string[] Targets
        {
            get
            {
                return _targets;
            }
        }

        /// <summary>
        /// Returns true if this request must wait for its results in order to complete.
        /// </summary>
        public bool ResultsNeeded
        {
            get
            {
                return _resultsNeeded;
            }
        }

        /// <summary>
        /// Implementation of the equality operator.
        /// </summary>
        /// <param name="left">The left hand argument</param>
        /// <param name="right">The right hand argument</param>
        /// <returns>True if the objects are equivalent, false otherwise.</returns>
        public static bool operator ==(FullyQualifiedBuildRequest left, FullyQualifiedBuildRequest right)
        {
            if (Object.ReferenceEquals(left, null))
            {
                if (Object.ReferenceEquals(right, null))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (Object.ReferenceEquals(right, null))
                {
                    return false;
                }
                else
                {
                    return left.InternalEquals(right);
                }
            }
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
        /// Returns the hashcode for this object.
        /// </summary>
        /// <returns>The hashcode</returns>
        public override int GetHashCode()
        {
            return _requestConfiguration.GetHashCode();
        }

        /// <summary>
        /// Determines equivalence between this object and another.
        /// </summary>
        /// <param name="obj">The object to which this one should be compared.</param>
        /// <returns>True if the objects are equivalent, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (null == obj)
            {
                return false;
            }

            if (this.GetType() != obj.GetType())
            {
                return false;
            }

            return InternalEquals((FullyQualifiedBuildRequest)obj);
        }

        /// <summary>
        /// Determines equivalence with another object of the same type.
        /// </summary>
        /// <param name="other">The other object with which to compare this one.</param>
        /// <returns>True if the objects are equivalent, false otherwise.</returns>
        private bool InternalEquals(FullyQualifiedBuildRequest other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (_requestConfiguration != other._requestConfiguration)
            {
                return false;
            }

            if (_resultsNeeded != other._resultsNeeded)
            {
                return false;
            }

            if (_targets.Length != other._targets.Length)
            {
                return false;
            }

            for (int i = 0; i < _targets.Length; ++i)
            {
                if (_targets[i] != other._targets[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
