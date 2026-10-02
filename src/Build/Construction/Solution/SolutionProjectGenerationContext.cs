// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Construction
{
    internal readonly struct SolutionProjectGenerationContext
    {
        internal SolutionProjectGenerationContext(
            ILoggingService loggingService,
            BuildEventContext buildEventContext,
            ISdkResolverService sdkResolverService,
            IReadOnlyCollection<string> targetNames,
            string toolsVersionOverride,
            int submissionId)
        {
            LoggingService = loggingService;
            BuildEventContext = buildEventContext;
            SdkResolverService = sdkResolverService;
            TargetNames = targetNames;
            ToolsVersionOverride = toolsVersionOverride;
            SubmissionId = submissionId;
        }

        internal ILoggingService LoggingService { get; }

        internal BuildEventContext BuildEventContext { get; }

        internal ISdkResolverService SdkResolverService { get; }

        internal IReadOnlyCollection<string> TargetNames { get; }

        internal string ToolsVersionOverride { get; }

        internal int SubmissionId { get; }
    }
}