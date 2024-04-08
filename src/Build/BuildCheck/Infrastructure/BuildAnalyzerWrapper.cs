// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.BuildCheck.Infrastructure;

/// <summary>
/// A wrapping, enriching class for BuildAnalyzer - so that we have additional data and functionality.
/// </summary>
internal sealed class BuildAnalyzerWrapper
{
    private readonly Stopwatch _stopwatch = new Stopwatch();

    public BuildAnalyzerWrapper(BuildAnalyzer buildAnalyzer)
    {
        BuildAnalyzer = buildAnalyzer;
    }

    internal BuildAnalyzer BuildAnalyzer { get; }
    private bool _isInitialized = false;

    // Let's optimize for the scenario where users have a single .editorconfig file that applies to the whole solution.
    // In such case - configuration will be same for all projects. So we do not need to store it per project in a collection.
    internal BuildAnalyzerConfigurationInternal? CommonConfig { get; private set; }

    // start new project
    internal void StartNewProject(
        string fullProjectPath,
        IReadOnlyList<BuildAnalyzerConfigurationInternal> userConfigs)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            CommonConfig = userConfigs[0];

            if (userConfigs.Count == 1)
            {
                return;
            }
        }

        // The Common configuration is not common anymore - let's nullify it and we will need to fetch configuration per project.
        if (CommonConfig == null || !userConfigs.All(t => t.IsSameConfigurationAs(CommonConfig)))
        {
            CommonConfig = null;
        }
    }

    // to be used on eval node (BuildCheckDataSource.BuildExecution)
    internal void Uninitialize()
    {
        _isInitialized = false;
    }

    internal TimeSpan Elapsed => _stopwatch.Elapsed;

    internal void ClearStats() => _stopwatch.Reset();

    internal CleanupScope StartSpan()
    {
        _stopwatch.Start();
        return new CleanupScope(_stopwatch.Stop);
    }

    internal readonly struct CleanupScope(Action disposeAction) : IDisposable
    {
        public void Dispose() => disposeAction();
    }
}
