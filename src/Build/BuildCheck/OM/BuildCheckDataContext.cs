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
/// <param name="projectConfigurationId">The unique id of a project with unique global properties set.</param>
public abstract class CheckData(string projectFilePath, int? projectConfigurationId)
{
    private string? _projectFileDirectory;
    // The id is going to be used in future revision
#pragma warning disable CA1823
    private int? _projectConfigurationId = projectConfigurationId;
#pragma warning restore CA1823

    /// <summary>
    /// Full path to the project file being built.
    /// </summary>
    public string ProjectFilePath { get; } = projectFilePath;

    // TBD: ProjectConfigurationId is not yet populated - as we need to properly anchor project build events
    ///// <summary>
    ///// The unique id of a project with unique global properties set.
    ///// This is helpful to distinguish between different configurations of a single project in case of multitargeting.
    /////
    ///// In cases where the project instance cannot be determined, it will be set to <see cref="BuildEventContext.InvalidProjectInstanceId"/>.
    ///// This is generally case of all evaluation-time data. To relate evaluation-time and build-execution-time data, use (TBD: ProjectStarted event/data)
    ///// </summary>
    ///// <remarks>
    ///// The same project with same global properties (aka configuration), can be executed multiple times to obtain results for multiple targets.
    /////  (this is internally distinguished as 'ProjectContextId' - each context is a different request for different targets results).
    ///// This is not distinguished by the ProjectConfigurationId - as all of those executions share same configuration and results and prevents re-execution of the same targets.
    /////
    ///// InstanceId (ConfigurationId): https://github.com/dotnet/msbuild/blob/2a8b16dbabd25782554ff0fe77619d58eccfe603/src/Build/BackEnd/BuildManager/BuildManager.cs#L2186-L2244
    ///// </remarks>
    ////public int ProjectConfigurationId { get; } = projectConfigurationId ?? BuildEventContext.InvalidProjectInstanceId;

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
public class BuildCheckDataContext<T> where T : CheckData
{
    private readonly BuildExecutionCheckWrapper _executionCheckWrapper;
    private readonly ICheckContext _checkContext;
    private readonly BuildAnalyzerConfigurationEffective[] _configPerRule;
    private readonly Action<BuildExecutionCheckWrapper, ICheckContext, BuildAnalyzerConfigurationEffective[], BuildCheckResult> _resultHandler;

    internal BuildCheckDataContext(
        BuildExecutionCheckWrapper analyzerWrapper,
        ICheckContext loggingContext,
        BuildExecutionCheckConfigurationEffective[] configPerRule,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult> resultHandler,
        T data)
    {
        _executionCheckWrapper = analyzerWrapper;
        _executionCheckContext = loggingContext;
        _configPerRule = configPerRule;
        _resultHandler = resultHandler;
        Data = data;
    }

    /// <summary>
    /// Method for reporting the result of the build analyzer rule.
    /// </summary>
    /// <param name="result"></param>
    public void ReportResult(BuildCheckResult result)
        => _resultHandler(_executionCheckWrapper, _checkContext, _configPerRule, result);

    /// <summary>
    /// Data to be analyzed.
    /// </summary>
    public T Data { get; }
}
