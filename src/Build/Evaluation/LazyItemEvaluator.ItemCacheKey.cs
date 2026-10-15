// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Identifies a cached snapshot by its ending operation and later Remove glob pattern contents.
    /// </summary>
    private readonly record struct ItemCacheKey(ItemOperation Operation, ImmutableHashSet<string> GlobsToIgnore)
    {
        public bool Equals(ItemCacheKey other)
        {
            if (!ReferenceEquals(Operation, other.Operation))
            {
                return false;
            }

            return ReferenceEquals(GlobsToIgnore, other.GlobsToIgnore)
                || (GlobsToIgnore.Count == other.GlobsToIgnore.Count && GlobsToIgnore.SetEquals(other.GlobsToIgnore));
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int globsHashCode = GlobsToIgnore.Count;

            // Exclusion order must not affect the key, but the operation must match by identity.
            foreach (string glob in GlobsToIgnore)
            {
                globsHashCode ^= FileUtilities.PathComparer.GetHashCode(glob);
            }

            return unchecked((Operation.GetHashCode() * -1521134295) + globsHashCode);
        }
    }
}
