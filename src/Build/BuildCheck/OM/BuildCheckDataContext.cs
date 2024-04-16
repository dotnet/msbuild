// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Base for a data passed from infrastructure to build analyzers.
/// </summary>
/// <param name="projectFilePath">Currently build project.</param>
public abstract class AnalysisData(string projectFilePath)
{
    /// <summary>
    /// Full path to the project file being built.
    /// </summary>
    public string ProjectFilePath { get; } = projectFilePath;
}

/// <summary>
/// Data passed from infrastructure to build analyzers.
/// </summary>
/// <typeparam name="T">The type of the actual data for analysis.</typeparam>
public class BuildCheckDataContext<T> where T : AnalysisData
{
    private readonly BuildAnalyzerWrapper _analyzerWrapper;
    private readonly LoggingContext _loggingContext;
    private readonly BuildAnalyzerConfigurationInternal[] _configPerRule;
    private readonly Action<BuildAnalyzerWrapper, LoggingContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult> _resultHandler;

    internal BuildCheckDataContext(
        BuildAnalyzerWrapper analyzerWrapper,
        LoggingContext loggingContext,
        BuildAnalyzerConfigurationInternal[] configPerRule,
        Action<BuildAnalyzerWrapper, LoggingContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult> resultHandler,
        T data)
    {
        _analyzerWrapper = analyzerWrapper;
        _loggingContext = loggingContext;
        _configPerRule = configPerRule;
        _resultHandler = resultHandler;
        Data = data;
    }

    /// <summary>
    /// Method for reporting the result of the build analyzer rule.
    /// </summary>
    /// <param name="result"></param>
    public void ReportResult(BuildCheckResult result)
        => _resultHandler(_analyzerWrapper, _loggingContext, _configPerRule, result);

    /// <summary>
    /// Data to be analyzed.
    /// </summary>
    public T Data { get; }
}
