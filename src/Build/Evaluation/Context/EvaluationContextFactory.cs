// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using EvaluationContextFactoryBase = Microsoft.Build.Framework.EvaluationContext.EvaluationContextFactory;

namespace Microsoft.Build.Evaluation.Context
{
    internal sealed class EvaluationContextFactory : EvaluationContextFactoryBase
    {
        public override Framework.EvaluationContext.EvaluationContext CreateContext()
        {
            return new EvaluationContext();
        }

        public override Framework.EvaluationContext.EvaluationContext CreateNullContext()
        {
            return new NullEvaluationContext();
        }
    }
}
