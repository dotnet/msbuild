// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.Build.Experimental;

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
