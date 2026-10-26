// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Identifies a cached snapshot by its ending operation and later Remove glob pattern contents.
    /// </summary>
    private readonly record struct ItemCacheKey(ItemOperation Operation, RemoveGlobPatterns GlobsToIgnore)
    {
        public bool Equals(ItemCacheKey other)
        {
            if (!ReferenceEquals(Operation, other.Operation))
            {
                return false;
            }

            return GlobsToIgnore.Equals(other.GlobsToIgnore);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return unchecked((Operation.GetHashCode() * -1521134295) + GlobsToIgnore.GetHashCode());
        }
    }
}
