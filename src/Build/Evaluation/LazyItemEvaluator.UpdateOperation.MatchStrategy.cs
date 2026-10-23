// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    private partial class UpdateOperation
    {
        /// <summary>
        ///  Selects how an Update matches items and resolves qualified metadata.
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
            ///  Matches the item specification without selecting items for qualified metadata.
            /// </summary>
            Specification,

            /// <summary>
            ///  Finds the last matching item of each item type for qualified metadata, using a literal
            ///  item index when possible and ordered matching otherwise.
            /// </summary>
            QualifiedUpdate,
        }
    }
}
