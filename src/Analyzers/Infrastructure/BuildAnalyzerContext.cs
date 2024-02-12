// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Experimental;

public delegate void EvaluatedPropertiesAction(EvaluatedPropertiesContext context);
public delegate void ParsedItemsAction(ParsedItemsContext context);

internal sealed class CentralBuildAnalyzerContext
{
    private EvaluatedPropertiesAction? _evaluatedPropertiesActions;
    private ParsedItemsAction? _parsedItemsActions;

    // This we can potentially use to subscribe for receiving evaluated props in the
    //  build event args. However - this needs to be done early on, when analyzers might not be known yet
    internal bool HasEvaluatedPropertiesActions => _evaluatedPropertiesActions != null;
    internal bool HasParsedItemsActions => _parsedItemsActions != null;

    internal void RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction evaluatedPropertiesAction)
    {
        // Here we might want to communicate to node that props need to be sent.
        //  (it was being communicated via MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION)
        _evaluatedPropertiesActions += evaluatedPropertiesAction;
    }

    internal void RegisterParsedItemsAction(ParsedItemsAction parsedItemsAction)
    {
        _parsedItemsActions += parsedItemsAction;
    }

    internal void RunEvaluatedPropertiesActions(EvaluatedPropertiesContext evaluatedPropertiesContext)
    {
        _evaluatedPropertiesActions?.Invoke(evaluatedPropertiesContext);
    }

    internal void RunParsedItemsActions(ParsedItemsContext parsedItemsContext)
    {
        _parsedItemsActions?.Invoke(parsedItemsContext);
    }
}

internal sealed class BuildAnalyzerTracingWrapper
{
    private readonly Stopwatch _stopwatch = new Stopwatch();

    public BuildAnalyzerTracingWrapper(BuildAnalyzer buildAnalyzer)
        => BuildAnalyzer = buildAnalyzer;

    internal BuildAnalyzer BuildAnalyzer { get; }

    internal TimeSpan Elapsed => _stopwatch.Elapsed;

    internal IDisposable StartSpan()
    {
        _stopwatch.Start();
        return new CleanupScope(_stopwatch.Stop);
    }

    internal readonly struct CleanupScope(Action disposeAction) : IDisposable
    {
        public void Dispose() => disposeAction();
    }
}

public interface IBuildAnalyzerContext
{
    void RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction evaluatedPropertiesAction);
    void RegisterParsedItemsAction(ParsedItemsAction parsedItemsAction);
}

internal sealed class BuildAnalyzerContext(BuildAnalyzerTracingWrapper analyzer, CentralBuildAnalyzerContext centralContext) : IBuildAnalyzerContext
{
    public void RegisterEvaluatedPropertiesAction(EvaluatedPropertiesAction evaluatedPropertiesAction)
    {
        void WrappedEvaluatedPropertiesAction(EvaluatedPropertiesContext context)
        {
            using var _ = analyzer.StartSpan();
            evaluatedPropertiesAction(context);
        }

        centralContext.RegisterEvaluatedPropertiesAction(WrappedEvaluatedPropertiesAction);
    }

    public void RegisterParsedItemsAction(ParsedItemsAction parsedItemsAction)
    {
        void WrappedParsedItemsAction(ParsedItemsContext context)
        {
            using var _ = analyzer.StartSpan();
            parsedItemsAction(context);
        }

        centralContext.RegisterParsedItemsAction(WrappedParsedItemsAction);
    }
}
