// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using System;

namespace Microsoft.Build.Framework.EvaluationContext
{
    /// <summary>
    /// An object used by the caller to extend the lifespan of evaluation caches (by passing the object on to other evaluations).
    /// The caller should throw away the context when the environment changes (IO, environment variables, SDK resolution inputs, etc).
    /// </summary>
    public abstract class EvaluationContext
    {
    }
}
