// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Components.Caching;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCop.Analyzers;
using Microsoft.Build.BuildCop.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCop;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCop.Infrastructure;

internal class BuildEventsProcessor(BuildCopCentralContext buildCopCentralContext)
{
    private readonly SimpleProjectRootElementCache _cache = new SimpleProjectRootElementCache();
    private readonly BuildCopCentralContext _buildCopCentralContext = buildCopCentralContext;

    // This requires MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION set to 1
    public void ProcessEvaluationFinishedEventArgs(
        IBuildAnalysisLoggingContext buildAnalysisContext,
        ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs)
    {
        LoggingContext loggingContext = buildAnalysisContext.ToLoggingContext();

        Dictionary<string, string> propertiesLookup = new Dictionary<string, string>();
        Internal.Utilities.EnumerateProperties(evaluationFinishedEventArgs.Properties, propertiesLookup,
            static (dict, kvp) => dict.Add(kvp.Key, kvp.Value));

        EvaluatedPropertiesAnalysisData analysisData =
            new(evaluationFinishedEventArgs.ProjectFile!, propertiesLookup);

        _buildCopCentralContext.RunEvaluatedPropertiesActions(analysisData, loggingContext, ReportResult);

        if (_buildCopCentralContext.HasParsedItemsActions)
        {
            ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(
                evaluationFinishedEventArgs.ProjectFile!, /*unused*/
                null, /*unused*/null, _cache, false /*Not explicitly loaded - unused*/);

            ParsedItemsAnalysisData itemsAnalysisData = new(evaluationFinishedEventArgs.ProjectFile!,
                new ItemsHolder(xml.Items, xml.ItemGroups));

            _buildCopCentralContext.RunParsedItemsActions(itemsAnalysisData, loggingContext, ReportResult);
        }
    }

    private static void ReportResult(
        BuildAnalyzerWrapper analyzerWrapper,
        LoggingContext loggingContext,
        BuildAnalyzerConfigurationInternal[] configPerRule,
        BuildCopResult result)
    {
        if (!analyzerWrapper.BuildAnalyzer.SupportedRules.Contains(result.BuildAnalyzerRule))
        {
            loggingContext.LogErrorFromText(null, null, null,
                BuildEventFileInfo.Empty,
                $"The analyzer '{analyzerWrapper.BuildAnalyzer.FriendlyName}' reported a result for a rule '{result.BuildAnalyzerRule.Id}' that it does not support.");
            return;
        }

        BuildAnalyzerConfigurationInternal config = configPerRule.Length == 1
            ? configPerRule[0]
            : configPerRule.First(r =>
                r.RuleId.Equals(result.BuildAnalyzerRule.Id, StringComparison.CurrentCultureIgnoreCase));

        if (!config.IsEnabled)
        {
            return;
        }

        BuildEventArgs eventArgs = result.ToEventArgs(config.Severity);
        eventArgs.BuildEventContext = loggingContext.BuildEventContext;
        loggingContext.LogBuildEvent(eventArgs);
    }
}
