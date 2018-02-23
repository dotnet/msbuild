// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using Microsoft.Build.BackEnd.SdkResolution;
using PublicEvaluationContext = Microsoft.Build.Framework.EvaluationContext.EvaluationContext;

namespace Microsoft.Build.Evaluation.Context
{
    internal abstract class EvaluationContextBase : PublicEvaluationContext
    {
        public abstract ISdkResolverService SdkResolverService { get; }
    }
}
