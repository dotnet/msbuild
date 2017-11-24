// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Components.Logging
{
    /// <summary>
    ///     Logging context and helpers for evaluation logging
    /// </summary>
    internal class EvaluationLoggingContext : LoggingContext
    {
        private readonly string _projectFile;

        public EvaluationLoggingContext(ILoggingService loggingService, BuildEventContext buildEventContext, string projectFile) :
            base(
                loggingService,
                loggingService.LogProjectEvaluationStarted(buildEventContext.NodeId, buildEventContext.SubmissionId, projectFile))
        {
            _projectFile = projectFile;
            IsValid = true;
        }

        /// <summary>
        /// Log that the project has finished
        /// </summary>
        internal void LogProjectEvaluationFinished()
        {
            ErrorUtilities.VerifyThrow(IsValid, "invalid");
            LoggingService.LogProjectEvaluationFinished(BuildEventContext, _projectFile);
            IsValid = false;
        }
    }
}
