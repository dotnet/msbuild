// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation.Context
{
    /// <summary>
    /// An object used by the caller to extend the lifespan of evaluation caches (by passing the object on to other evaluations).
    /// The caller should throw away the context when the environment changes (IO, environment variables, SDK resolution inputs, etc).
    /// </summary>
    public class EvaluationContext
    {
        public enum SharingPolicy
        {
            Shared,
            Isolated
        }

        internal virtual ISdkResolverService SdkResolverService { get; } = new SdkResolverCachingWrapper(new SdkResolverService());

        internal EvaluationContext()
        {
        }

        /// <summary>
        /// Factory for <see cref="EvaluationContext"/>
        /// </summary>
        public static EvaluationContext Create(SharingPolicy policy)
        {
            switch (policy)
            {
                case SharingPolicy.Shared:
                    return new EvaluationContext();
                case SharingPolicy.Isolated:
                    return new IsolatedEvaluationContext();
                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    return null;
            }
        }
    }
}
