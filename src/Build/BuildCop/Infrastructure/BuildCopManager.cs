// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCop.Analyzers;
using Microsoft.Build.BuildCop.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCop;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildCop.Infrastructure;

internal sealed class BuildCopManager : IBuildCopManager
{
    private readonly List<BuildAnalyzerTracingWrapper> _analyzers = new();
    private readonly BuildCopCentralContext _buildCopCentralContext = new();

    private BuildCopManager() { }

    internal static IBuildCopManager Instance => CreateBuildAnalysisManager();

    public void RegisterAnalyzer(BuildAnalyzer analyzer)
    {
        if (!analyzer.SupportedRules.Any())
        {
            // error out
            return;
        }

        IEnumerable<BuildAnalyzerConfigurationInternal> configuration = analyzer.SupportedRules.Select(ConfigurationProvider.GetMergedConfiguration);

        if (configuration.All(c => !c.IsEnabled))
        {
            return;
        }

        // TODO: the config module should return any possible user configurations per rule
        ConfigurationContext configurationContext = ConfigurationContext.Null;
        analyzer.Initialize(configurationContext);
        var wrappedAnalyzer = new BuildAnalyzerTracingWrapper(analyzer);
        var wrappedContext = new BuildCopContext(wrappedAnalyzer, _buildCopCentralContext);
        analyzer.RegisterActions(wrappedContext);
        _analyzers.Add(wrappedAnalyzer);
    }

    // TODO: all this processing should be queued and done async. We might even want to run analyzers in parallel

    private SimpleProjectRootElementCache _cache = new SimpleProjectRootElementCache();

    // This requires MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION set to 1
    public void ProcessEvaluationFinishedEventArgs(IBuildAnalysisLoggingContext buildAnalysisContext,
        ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs)
    {
        LoggingContext loggingContext = buildAnalysisContext.ToLoggingContext();

        Dictionary<string, string> propertiesLookup = new Dictionary<string, string>();
        Internal.Utilities.EnumerateProperties(evaluationFinishedEventArgs.Properties, propertiesLookup,
            static (dict, kvp) => dict.Add(kvp.Key, kvp.Value));

        EvaluatedPropertiesContext context = new EvaluatedPropertiesContext(loggingContext,
            new ReadOnlyDictionary<string, string>(propertiesLookup),
            evaluationFinishedEventArgs.ProjectFile!);

        _buildCopCentralContext.RunEvaluatedPropertiesActions(context);

        if (_buildCopCentralContext.HasParsedItemsActions)
        {
            ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(evaluationFinishedEventArgs.ProjectFile!, /*unused*/
                null, /*unused*/null, _cache, false /*Not explicitly loaded - unused*/);

            ParsedItemsContext parsedItemsContext = new ParsedItemsContext(loggingContext,
                new ItemsHolder(xml.Items, xml.ItemGroups));

            _buildCopCentralContext.RunParsedItemsActions(parsedItemsContext);
        }
    }

    // TODO: tracing: https://github.com/dotnet/msbuild/issues/9629
    // should have infra as well, should log to BuildCopConnectorLogger upon shutdown (if requested)
    public string CreateTracingStats()
    {
        return string.Join(Environment.NewLine,
            _analyzers.Select(a => GetAnalyzerDescriptor(a.BuildAnalyzer) + ": " + a.Elapsed));

        string GetAnalyzerDescriptor(BuildAnalyzer buildAnalyzer)
            => buildAnalyzer.FriendlyName + " (" + buildAnalyzer.GetType() + ")";
    }

    internal static BuildCopManager CreateBuildAnalysisManager()
    {
        var buildAnalysisManager = new BuildCopManager();
        buildAnalysisManager.RegisterAnalyzer(new SharedOutputPathAnalyzer());
        // ... Register other internal analyzers
        return buildAnalysisManager;
    }
}
