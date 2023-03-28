// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Shared;

#nullable disable

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
                loggingService.CreateEvaluationBuildEventContext(buildEventContext.NodeId, buildEventContext.SubmissionId))
        {
            _projectFile = projectFile;
            IsValid = true;
        }

        public void LogProjectEvaluationStarted()
        {
            LoggingService.LogProjectEvaluationStarted(BuildEventContext, _projectFile);
        }

        /// <summary>
        /// Log that the project has finished
        /// </summary>
        internal void LogProjectEvaluationFinished(IEnumerable globalProperties, IEnumerable properties, IEnumerable items, ProfilerResult? profilerResult)
        {
            ErrorUtilities.VerifyThrow(IsValid, "invalid");
            LoggingService.LogProjectEvaluationFinished(BuildEventContext, _projectFile, globalProperties, properties, items, profilerResult);
            IsValid = false;
        }
    }
}
