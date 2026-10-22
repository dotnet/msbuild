// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Captures the source element and condition result of an Include operation for this evaluation.
    /// </summary>
    /// <param name="itemElement">The Include element that produced the items.</param>
    /// <param name="conditionResult">Whether both the item-group and item conditions evaluated to <see langword="true"/>.</param>
    /// <remarks>
    ///  The index in the evaluator's Include table identifies this operation and its declaration order.
    ///  Replays and metadata-only clones retain that identity rather than reevaluating the conditions.
    /// </remarks>
    public readonly struct IncludeData(ProjectItemElement itemElement, bool conditionResult)
    {
        /// <summary>
        ///  Gets the Include element that originally produced the items, rather than a subsequent Update element.
        /// </summary>
        public ProjectItemElement ItemElement => itemElement;

        /// <summary>
        ///  Gets whether the originating item-group and item conditions were both <see langword="true"/>.
        /// </summary>
        public bool ConditionResult => conditionResult;
    }
}
