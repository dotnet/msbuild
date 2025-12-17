// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Components.Logging
{
    /// <summary>
    ///     Logging context and helpers for evaluation logging.
    /// </summary>
    internal class EvaluationLoggingContext : LoggingContext
    {
        private readonly string _projectFile;

        public EvaluationLoggingContext(ILoggingService loggingService, BuildEventContext buildEventContext, string projectFile)
            : base(
                loggingService,
                loggingService.CreateEvaluationBuildEventContext(buildEventContext))
        {
            _projectFile = projectFile;
            IsValid = true;
        }

        public void LogProjectEvaluationStarted()
        {
            LoggingService.LogProjectEvaluationStarted(BuildEventContext, _projectFile);
            LoggingService.BuildEngineDataRouter.ProcessProjectEvaluationStarted(
                new CheckLoggingContext(LoggingService, BuildEventContext), _projectFile);
        }

        /// <summary>
        /// Logs that the project evaluation has finished.
        /// </summary>
        /// <param name="globalProperties">Global properties used in the project evaluation.</param>
        /// <param name="properties">Properties used in the project evaluation.</param>
        /// <param name="items">Items used in the project evaluation.</param>
        /// <param name="profilerResult">Parameter contains the profiler result of the project evaluation.</param>
        internal void LogProjectEvaluationFinished(IEnumerable globalProperties, IEnumerable properties, IEnumerable items, ProfilerResult? profilerResult)
        {
            ErrorUtilities.VerifyThrow(IsValid, "invalid");
            LoggingService.LogProjectEvaluationFinished(BuildEventContext, _projectFile, globalProperties, properties, items, profilerResult);
            IsValid = false;
        }
    }
}
