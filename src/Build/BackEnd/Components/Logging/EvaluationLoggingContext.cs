// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using Microsoft.Build.Framework;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Logging context and helpers for evaluation logging
    /// </summary>
    internal class EvaluationLoggingContext : BaseLoggingContext
    {
        public EvaluationLoggingContext(ILoggingService loggingService, BuildEventContext eventContext) : base(loggingService, eventContext)
        {
            IsValid = true;
        }
    }
}
