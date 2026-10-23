// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private partial class UpdateOperation
    {
        /// <summary>
        ///  Selects how an Update matches candidate items and captures sources for qualified metadata.
        /// </summary>
        private enum MatchStrategy
        {
            /// <summary>
            ///  Matching has not yet been prepared.
            /// </summary>
            Uninitialized,

            /// <summary>
            ///  Matches every current item for a single bare reference to the updated item type.
            /// </summary>
            All,

            /// <summary>
            ///  Matches the item specification without capturing sources for qualified metadata.
            /// </summary>
            Specification,

            /// <summary>
            ///  Matches and captures the last matching source of each referenced item type, using a literal
            ///  source index when possible and ordered source matching otherwise.
            /// </summary>
            Capture,
        }
    }
}
