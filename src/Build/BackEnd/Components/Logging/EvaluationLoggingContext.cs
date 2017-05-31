// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Components.Logging
{
    /// <summary>
    ///     Logging context and helpers for evaluation logging
    /// </summary>
    internal class EvaluationLoggingContext : LoggingContext
    {
        public EvaluationLoggingContext(ILoggingService loggingService, BuildEventContext eventContext, int evaluationId) : base(
            loggingService,
            new BuildEventContext(
                eventContext.SubmissionId,
                eventContext.NodeId,
                evaluationId,
                eventContext.ProjectInstanceId,
                eventContext.ProjectContextId,
                eventContext.TargetId,
                eventContext.TaskId
            ))
        {
            IsValid = true;
        }
    }
}
