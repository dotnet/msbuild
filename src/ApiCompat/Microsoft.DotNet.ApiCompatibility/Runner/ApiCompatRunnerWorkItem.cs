// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Runner
{
    /// <summary>
    /// Work item for the api compat runner
    /// </summary>
    public readonly struct ApiCompatRunnerWorkItem : IEquatable<ApiCompatRunnerWorkItem>
    {
        /// <summary>
        /// The metadata information of the left assemblies to compare with the rights.
        /// </summary>
        public readonly IEnumerable<MetadataInformation> Lefts;

        /// <summary>
        /// The api compat options to configure the comparison checks.
        /// </summary>
        public readonly ApiCompatRunnerOptions Options;

        /// <summary>
        /// The metadata information of the right assemblies that are compared against the lefts.
        /// </summary>
        public HashSet<MetadataInformation> Rights { get; }

        /// <summary>
        /// Initializes an api compat work item.
        /// </summary>
        public ApiCompatRunnerWorkItem(IEnumerable<MetadataInformation> lefts,
            ApiCompatRunnerOptions options,
            IEnumerable<MetadataInformation> rights)
        {
            Lefts = lefts;
            Options = options;
            Rights = new HashSet<MetadataInformation>(rights);
        }

        public ApiCompatRunnerWorkItem(MetadataInformation left,
            ApiCompatRunnerOptions options,
            params MetadataInformation[] rights)
            : this(new MetadataInformation[] { left }, options, rights)
        {
        }

        /// <inheritdoc />
        public bool Equals(ApiCompatRunnerWorkItem other) => other.Lefts.SequenceEqual(Lefts) && other.Options.Equals(Options);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ApiCompatRunnerWorkItem item && Equals(item);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 19 + Options.GetHashCode();
                foreach (MetadataInformation left in Lefts)
                {
                    hash = hash * 31 + left.GetHashCode();
                }
                return hash;
            }
        }

        /// <inheritdoc />
        public static bool operator ==(ApiCompatRunnerWorkItem workItem1, ApiCompatRunnerWorkItem workItem2) => workItem1.Equals(workItem2);

        /// <inheritdoc />
        public static bool operator !=(ApiCompatRunnerWorkItem workItem1, ApiCompatRunnerWorkItem workItem2) => !(workItem1 == workItem2);

        /// <inheritdoc />
        public override string ToString() => $"{Lefts.Select(l => l.AssemblyId).Aggregate((l1, l2) => l1 + ", " + l2)}: {Options}";
    }
}
