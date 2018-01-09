// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

namespace Microsoft.Build.Framework.EvaluationContext
{
    /// <summary>
    /// Factory for <see cref="EvaluationContext"/>
    /// </summary>
    public abstract class EvaluationContextFactory
    {
        public abstract EvaluationContext CreateContext();

        public abstract EvaluationContext CreateNullContext();
    }
}
