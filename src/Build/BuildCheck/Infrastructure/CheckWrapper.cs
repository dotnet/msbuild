// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// A wrapping, enriching class for BuildCheck - so that we have additional data and functionality.
/// </summary>
internal sealed class CheckWrapper
{
    private readonly Stopwatch _stopwatch = new Stopwatch();

    /// <summary>
    /// Maximum amount of messages that could be sent per check rule.
    /// </summary>
    public const int MaxReportsNumberPerRule = 10;

    /// <summary>
    /// Keeps track of number of reports sent per rule.
    /// </summary>
    private Dictionary<string, int>? _reportsCountPerRule;

    private readonly bool _limitReportsNumber;

    public CheckWrapper(Check check)
    {
        Check = check;
        _limitReportsNumber = !Traits.Instance.EscapeHatches.DoNotLimitBuildCheckResultsNumber;
    }

    internal Check Check { get; }
    private bool _areStatsInitialized = false;

    // Let's optimize for the scenario where users have a single .editorconfig file that applies to the whole solution.
    // In such case - configuration will be same for all projects. So we do not need to store it per project in a collection.
    internal CheckConfigurationEffective? CommonConfig { get; private set; }

    internal void Initialize()
    {
        if (_limitReportsNumber)
        {
            _reportsCountPerRule = new Dictionary<string, int>();
        }
        _areStatsInitialized = false;
    }

    // start new project
    internal void StartNewProject(
        string fullProjectPath,
        IReadOnlyList<CheckConfigurationEffective> userConfigs)
    {
        if (!_areStatsInitialized)
        {
            _areStatsInitialized = true;
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

    internal void ReportResult(BuildCheckResult result, ICheckContext checkContext, CheckConfigurationEffective config)
    {
        if (_reportsCountPerRule is not null)
        {
            if (!_reportsCountPerRule.ContainsKey(result.CheckRule.Id))
            {
                _reportsCountPerRule[result.CheckRule.Id] = 0;
            }
            _reportsCountPerRule[result.CheckRule.Id]++;

            if (_reportsCountPerRule[result.CheckRule.Id] == MaxReportsNumberPerRule + 1)
            {
                checkContext.DispatchAsCommentFromText(MessageImportance.Normal, $"The check '{Check.FriendlyName}' has exceeded the maximum number of results allowed for the rule '{result.CheckRule.Id}'. Any additional results will not be displayed.");
                return;
            }

            if (_reportsCountPerRule[result.CheckRule.Id] > MaxReportsNumberPerRule + 1)
            {
                return;
            }
        }

        BuildEventArgs eventArgs = result.ToEventArgs(config.Severity);
        eventArgs.BuildEventContext = checkContext.BuildEventContext;
        checkContext.DispatchBuildEvent(eventArgs);
    }

    // to be used on eval node (BuildCheckDataSource.check)
    internal void UninitializeStats()
    {
        _areStatsInitialized = false;
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
