// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework;
using System.IO;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Base for a data passed from infrastructure to build analyzers.
/// </summary>
/// <param name="projectFilePath">Currently built project.</param>
public abstract class AnalysisData(string projectFilePath)
{
    private string? _projectFileDirectory;

    /// <summary>
    /// Full path to the project file being built.
    /// </summary>
    public string ProjectFilePath { get; } = projectFilePath;

    /// <summary>
    /// Directory path of the file being built (the containing directory of <see cref="ProjectFilePath"/>).
    /// </summary>
    public string ProjectFileDirectory =>
        _projectFileDirectory ??= Path.GetDirectoryName(ProjectFilePath)!;
}

/// <summary>
/// Data passed from infrastructure to build analyzers.
/// </summary>
/// <typeparam name="T">The type of the actual data for analysis.</typeparam>
public class BuildCheckDataContext<T> where T : AnalysisData
{
    private readonly BuildAnalyzerWrapper _analyzerWrapper;
    private readonly IAnalysisContext _analysisContext;
    private readonly BuildAnalyzerConfigurationInternal[] _configPerRule;
    private readonly Action<BuildAnalyzerWrapper, IAnalysisContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult> _resultHandler;

    internal BuildCheckDataContext(
        BuildAnalyzerWrapper analyzerWrapper,
        IAnalysisContext loggingContext,
        BuildAnalyzerConfigurationInternal[] configPerRule,
        Action<BuildAnalyzerWrapper, IAnalysisContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult> resultHandler,
        T data)
    {
        _analyzerWrapper = analyzerWrapper;
        _analysisContext = loggingContext;
        _configPerRule = configPerRule;
        _resultHandler = resultHandler;
        Data = data;
    }

    /// <summary>
    /// Method for reporting the result of the build analyzer rule.
    /// </summary>
    /// <param name="result"></param>
    public void ReportResult(BuildCheckResult result)
        => _resultHandler(_analyzerWrapper, _analysisContext, _configPerRule, result);

    /// <summary>
    /// Data to be analyzed.
    /// </summary>
    public T Data { get; }
}
