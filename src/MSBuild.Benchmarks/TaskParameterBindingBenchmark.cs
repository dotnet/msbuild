// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks the per-parameter binding cost the engine pays when converting a task
/// parameter's string value into the task's declared CLR type.
///
/// Each benchmark drives the real <see cref="TaskExecutionHost.SetTaskParameters"/> path
/// (expansion + type detection + conversion + property set) for a single scalar parameter,
/// so the new path-like types (<see cref="AbsolutePath"/>, <see cref="FileInfo"/>,
/// <see cref="DirectoryInfo"/>) and the strongly-typed <see cref="ITaskItem{T}"/> variants
/// can be compared against the pre-existing <c>string</c> and <see cref="ITaskItem"/> bindings.
///
/// Task-input logging is disabled (the unit-test host constructor leaves <c>LogTaskInputs</c>
/// false), so the measurements reflect binding work rather than logging overhead.
/// </summary>
[MemoryDiagnoser]
public class TaskParameterBindingBenchmark
{
    private const string TaskName = "BindingBenchmarkTask";

    private TaskExecutionHost _host = null!;
    private ProjectCollection _projectCollection = null!;

    private Dictionary<string, (string, ElementLocation)> _stringParam = null!;
    private Dictionary<string, (string, ElementLocation)> _itemParam = null!;
    private Dictionary<string, (string, ElementLocation)> _absolutePathParam = null!;
    private Dictionary<string, (string, ElementLocation)> _fileInfoParam = null!;
    private Dictionary<string, (string, ElementLocation)> _directoryInfoParam = null!;
    private Dictionary<string, (string, ElementLocation)> _taskItemAbsolutePathParam = null!;
    private Dictionary<string, (string, ElementLocation)> _taskItemFileInfoParam = null!;
    private Dictionary<string, (string, ElementLocation)> _taskItemDirectoryInfoParam = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _projectCollection = new ProjectCollection();
        ProjectRootElement xml = ProjectRootElement.Create(_projectCollection);
        xml.FullPath = Path.Combine(Path.GetTempPath(), "binding-benchmark.proj");
        xml.AddTarget("Build");
        ProjectInstance project = new(xml, globalProperties: null, toolsVersion: null, _projectCollection);

        ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        TargetLoggingContext targetLoggingContext = new(
            loggingService,
            new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

        _host = new TaskExecutionHost();

        AssemblyLoadInfo loadInfo = AssemblyLoadInfo.Create(typeof(BindingBenchmarkTaskFactory).FullName, null);
        LoadedType loadedType = new(
            typeof(BindingBenchmarkTaskFactory),
            loadInfo,
            typeof(BindingBenchmarkTaskFactory).Assembly,
            typeof(ITaskItem));

        BindingBenchmarkTaskFactory factory = new();
        _host._UNITTESTONLY_TaskFactoryWrapper = new TaskFactoryWrapper(factory, loadedType, TaskName, TaskHostParameters.Empty);

        StubBuildEngine buildEngine = new();
        _host.InitializeForTask(
            buildEngine,
            targetLoggingContext,
            project,
            TaskName,
            ElementLocation.Create("none", 1, 1),
            taskHost: null,
            continueOnError: false,
            projectFile: "binding-benchmark.proj",
#if FEATURE_APPDOMAIN
            null,
#endif
            hostServices: null,
            isOutOfProc: false,
            CancellationToken.None,
            TaskEnvironment.Fallback);

        _host.FindTask(TaskHostParameters.Empty);

        TaskLoggingContext taskLoggingContext = new(loggingService, targetLoggingContext.BuildEventContext);

        ItemDictionary<ProjectItemInstance> items = new();
        Lookup lookup = new(items, new PropertyDictionary<ProjectPropertyInstance>());
        ItemBucket bucket = new(FrozenSet<string>.Empty, new Dictionary<string, string>(), lookup, 0);
        bucket.Initialize(null);

        _host.InitializeForBatch(taskLoggingContext, bucket, TaskHostParameters.Empty, scheduledNodeId: 1);

        // Literal values with no expansion tokens, so the per-type conversion is the only variable.
        const string FilePath = "subdir/example.txt";
        const string DirPath = "subdir/nested";

        _stringParam = BuildParameter("StringParam", FilePath);
        _itemParam = BuildParameter("ItemParam", FilePath);
        _absolutePathParam = BuildParameter("AbsolutePathParam", FilePath);
        _fileInfoParam = BuildParameter("FileInfoParam", FilePath);
        _directoryInfoParam = BuildParameter("DirectoryInfoParam", DirPath);
        _taskItemAbsolutePathParam = BuildParameter("TaskItemAbsolutePathParam", FilePath);
        _taskItemFileInfoParam = BuildParameter("TaskItemFileInfoParam", FilePath);
        _taskItemDirectoryInfoParam = BuildParameter("TaskItemDirectoryInfoParam", DirPath);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        ((IDisposable)_host)?.Dispose();
        _projectCollection?.Dispose();
    }

    private static Dictionary<string, (string, ElementLocation)> BuildParameter(string name, string value)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            [name] = (value, ElementLocation.Create("binding-benchmark.proj", 1, 1)),
        };

    [Benchmark(Baseline = true)]
    public bool String() => _host.SetTaskParameters(_stringParam);

    [Benchmark]
    public bool Item() => _host.SetTaskParameters(_itemParam);

    [Benchmark]
    public bool AbsolutePath() => _host.SetTaskParameters(_absolutePathParam);

    [Benchmark]
    public bool FileInfo() => _host.SetTaskParameters(_fileInfoParam);

    [Benchmark]
    public bool DirectoryInfo() => _host.SetTaskParameters(_directoryInfoParam);

    [Benchmark]
    public bool TaskItemOfAbsolutePath() => _host.SetTaskParameters(_taskItemAbsolutePathParam);

    [Benchmark]
    public bool TaskItemOfFileInfo() => _host.SetTaskParameters(_taskItemFileInfoParam);

    [Benchmark]
    public bool TaskItemOfDirectoryInfo() => _host.SetTaskParameters(_taskItemDirectoryInfoParam);

    /// <summary>
    /// A minimal task exposing one parameter per binding type under test. Implements
    /// <see cref="IGeneratedTask"/> so the host's property setter is a constant cost across
    /// all parameters, leaving the per-type conversion as the only variable.
    /// </summary>
    private sealed class BindingBenchmarkTask : ITask, IGeneratedTask
    {
        public IBuildEngine BuildEngine { get; set; } = null!;

        public ITaskHost HostObject { get; set; } = null!;

        public string StringParam { get; set; } = null!;

        public ITaskItem ItemParam { get; set; } = null!;

        public AbsolutePath AbsolutePathParam { get; set; }

        public FileInfo FileInfoParam { get; set; } = null!;

        public DirectoryInfo DirectoryInfoParam { get; set; } = null!;

        public ITaskItem<AbsolutePath> TaskItemAbsolutePathParam { get; set; } = null!;

        public ITaskItem<FileInfo> TaskItemFileInfoParam { get; set; } = null!;

        public ITaskItem<DirectoryInfo> TaskItemDirectoryInfoParam { get; set; } = null!;

        public bool Execute() => true;

        public void SetPropertyValue(TaskPropertyInfo property, object value)
        {
            switch (property.Name)
            {
                case nameof(StringParam): StringParam = (string)value; break;
                case nameof(ItemParam): ItemParam = (ITaskItem)value; break;
                case nameof(AbsolutePathParam): AbsolutePathParam = (AbsolutePath)value; break;
                case nameof(FileInfoParam): FileInfoParam = (FileInfo)value; break;
                case nameof(DirectoryInfoParam): DirectoryInfoParam = (DirectoryInfo)value; break;
                case nameof(TaskItemAbsolutePathParam): TaskItemAbsolutePathParam = (ITaskItem<AbsolutePath>)value; break;
                case nameof(TaskItemFileInfoParam): TaskItemFileInfoParam = (ITaskItem<FileInfo>)value; break;
                case nameof(TaskItemDirectoryInfoParam): TaskItemDirectoryInfoParam = (ITaskItem<DirectoryInfo>)value; break;
                default: throw new ArgumentException($"Unknown property '{property.Name}'.", nameof(property));
            }
        }

        public object GetPropertyValue(TaskPropertyInfo property)
            => property.Name switch
            {
                nameof(StringParam) => StringParam,
                nameof(ItemParam) => ItemParam,
                nameof(AbsolutePathParam) => AbsolutePathParam,
                nameof(FileInfoParam) => FileInfoParam,
                nameof(DirectoryInfoParam) => DirectoryInfoParam,
                nameof(TaskItemAbsolutePathParam) => TaskItemAbsolutePathParam,
                nameof(TaskItemFileInfoParam) => TaskItemFileInfoParam,
                nameof(TaskItemDirectoryInfoParam) => TaskItemDirectoryInfoParam,
                _ => throw new ArgumentException($"Unknown property '{property.Name}'.", nameof(property)),
            };
    }

    /// <summary>
    /// Task factory which produces <see cref="BindingBenchmarkTask"/> instances and reports
    /// their public properties as task parameters.
    /// </summary>
    private sealed class BindingBenchmarkTaskFactory : ITaskFactory
    {
        public string FactoryName => nameof(BindingBenchmarkTaskFactory);

        public Type TaskType => typeof(BindingBenchmarkTask);

        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost) => true;

        public TaskPropertyInfo[] GetTaskParameters()
        {
            PropertyInfo[] properties = typeof(BindingBenchmarkTask).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            TaskPropertyInfo[] result = new TaskPropertyInfo[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                result[i] = new TaskPropertyInfo(
                    properties[i].Name,
                    properties[i].PropertyType,
                    output: properties[i].GetCustomAttributes(typeof(OutputAttribute), false).Length > 0,
                    required: properties[i].GetCustomAttributes(typeof(RequiredAttribute), false).Length > 0);
            }

            return result;
        }

        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) => new BindingBenchmarkTask();

        public void CleanupTask(ITask task)
        {
        }
    }

    /// <summary>
    /// A no-op build engine. None of its members are invoked while binding input parameters.
    /// </summary>
    private sealed class StubBuildEngine : IBuildEngine2
    {
        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => "binding-benchmark.proj";

        public bool IsRunningMultipleNodes => false;

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
            => throw new NotImplementedException();

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
            => throw new NotImplementedException();

        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
            => throw new NotImplementedException();
    }
}
