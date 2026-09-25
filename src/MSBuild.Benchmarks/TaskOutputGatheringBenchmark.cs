// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Frozen;
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
using BuildTaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using UtilityTaskItem = Microsoft.Build.Utilities.TaskItem;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks the engine cost of gathering large task item output arrays back into the current batch lookup.
/// </summary>
[MemoryDiagnoser]
public class TaskOutputGatheringBenchmark
{
    private const string ProjectFile = "output-gathering-benchmark.proj";
    private const string OutputItemType = "GatheredOutput";
    private const string TaskName = "OutputGatheringBenchmarkTask";

    private TaskExecutionHost _host = null!;
    private ProjectCollection _projectCollection = null!;
    private ProjectInstance _project = null!;
    private ILoggingService _loggingService = null!;
    private TargetLoggingContext _targetLoggingContext = null!;
    private TaskLoggingContext _taskLoggingContext = null!;
    private TaskOutputGatheringTaskFactory _factory = null!;

    [Params(1000, 10000, 50000)]
    public int ItemCount { get; set; }

    [Params(0, 5)]
    public int MetadataCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _projectCollection = new ProjectCollection();
        ProjectRootElement xml = ProjectRootElement.Create(_projectCollection);
        xml.FullPath = ProjectFile;
        xml.AddTarget("Build");
        _project = new ProjectInstance(xml, globalProperties: null, toolsVersion: null, _projectCollection);

        _loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        _targetLoggingContext = new TargetLoggingContext(
            _loggingService,
            new BuildEventContext(1, 1, BuildEventContext.InvalidProjectContextId, 1));

        _host = new TaskExecutionHost();

        AssemblyLoadInfo loadInfo = AssemblyLoadInfo.Create(typeof(TaskOutputGatheringTaskFactory).FullName, null);
        LoadedType loadedType = new(
            typeof(TaskOutputGatheringTaskFactory),
            loadInfo,
            typeof(TaskOutputGatheringTaskFactory).Assembly,
            typeof(ITaskItem));

        ITaskItem[] engineTaskItems = CreateEngineTaskItems();
        _factory = new TaskOutputGatheringTaskFactory(engineTaskItems, CreateMixedTaskItems(engineTaskItems), CreateUtilityTaskItems());
        _host._UNITTESTONLY_TaskFactoryWrapper = new TaskFactoryWrapper(_factory, loadedType, TaskName, TaskHostParameters.Empty);

        _host.InitializeForTask(
            new StubBuildEngine(),
            _targetLoggingContext,
            _project,
            TaskName,
            ElementLocation.Create(ProjectFile, 1, 1),
            taskHost: null,
            continueOnError: false,
            projectFile: ProjectFile,
#if FEATURE_APPDOMAIN
            null,
#endif
            hostServices: null,
            isOutOfProc: false,
            CancellationToken.None,
            TaskEnvironment.Fallback);

        _host.FindTask(TaskHostParameters.Empty);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        ((IDisposable)_host)?.Dispose();
        _projectCollection?.Dispose();
    }

    private void InitializeBatch()
    {
        ItemDictionary<ProjectItemInstance> items = new();
        Lookup lookup = new(items, new PropertyDictionary<ProjectPropertyInstance>());
        ItemBucket bucket = new(FrozenSet<string>.Empty, new Dictionary<string, string>(), lookup, 0);
        bucket.Initialize(null);

        _taskLoggingContext = new TaskLoggingContext(_loggingService, _targetLoggingContext.BuildEventContext);
        _host.InitializeForBatch(_taskLoggingContext, bucket, TaskHostParameters.Empty, scheduledNodeId: 1);
    }

    [Benchmark(Baseline = true)]
    public bool ProjectItemInstanceTaskItems()
    {
        InitializeBatch();
        return _host.GatherTaskOutputs(
            nameof(TaskOutputGatheringTask.ProjectItemInstanceTaskItemOutputs),
            ElementLocation.Create(ProjectFile, 1, 1),
            outputTargetIsItem: true,
            OutputItemType);
    }

    [Benchmark]
    public bool MixedTaskItemsOldPathApproximation()
    {
        InitializeBatch();
        return _host.GatherTaskOutputs(
            nameof(TaskOutputGatheringTask.MixedTaskItemOutputs),
            ElementLocation.Create(ProjectFile, 1, 1),
            outputTargetIsItem: true,
            OutputItemType);
    }

    [Benchmark]
    public bool UtilityTaskItems()
    {
        InitializeBatch();
        return _host.GatherTaskOutputs(
            nameof(TaskOutputGatheringTask.UtilityTaskItemOutputs),
            ElementLocation.Create(ProjectFile, 1, 1),
            outputTargetIsItem: true,
            OutputItemType);
    }

    private ITaskItem[] CreateEngineTaskItems()
    {
        var outputs = new ITaskItem[ItemCount];
        for (int i = 0; i < outputs.Length; i++)
        {
            var item = new ProjectItemInstance(_project, "Input", $"item{i}", ProjectFile);
            AddMetadata(item, i);
            outputs[i] = new BuildTaskItem(item);
        }

        return outputs;
    }

    private ITaskItem[] CreateMixedTaskItems(ITaskItem[] engineTaskItems)
    {
        var outputs = new ITaskItem[ItemCount];
        Array.Copy(engineTaskItems, outputs, engineTaskItems.Length);

        var utilityItem = new UtilityTaskItem("item0");
        for (int metadataIndex = 0; metadataIndex < MetadataCount; metadataIndex++)
        {
            utilityItem.SetMetadata($"Metadata{metadataIndex}", $"Value0_{metadataIndex}");
        }

        outputs[0] = utilityItem;
        return outputs;
    }

    private ITaskItem[] CreateUtilityTaskItems()
    {
        var outputs = new ITaskItem[ItemCount];
        for (int i = 0; i < outputs.Length; i++)
        {
            var item = new UtilityTaskItem($"item{i}");
            for (int metadataIndex = 0; metadataIndex < MetadataCount; metadataIndex++)
            {
                item.SetMetadata($"Metadata{metadataIndex}", $"Value{i}_{metadataIndex}");
            }

            outputs[i] = item;
        }

        return outputs;
    }

    private void AddMetadata(ProjectItemInstance item, int itemIndex)
    {
        for (int metadataIndex = 0; metadataIndex < MetadataCount; metadataIndex++)
        {
            item.SetMetadata($"Metadata{metadataIndex}", $"Value{itemIndex}_{metadataIndex}");
        }
    }

    private sealed class TaskOutputGatheringTask : ITask, IGeneratedTask
    {
        public TaskOutputGatheringTask(ITaskItem[] projectItemInstanceTaskItemOutputs, ITaskItem[] mixedTaskItemOutputs, ITaskItem[] utilityTaskItemOutputs)
        {
            ProjectItemInstanceTaskItemOutputs = projectItemInstanceTaskItemOutputs;
            MixedTaskItemOutputs = mixedTaskItemOutputs;
            UtilityTaskItemOutputs = utilityTaskItemOutputs;
        }

        public IBuildEngine BuildEngine { get; set; } = null!;

        public ITaskHost HostObject { get; set; } = null!;

        [Output]
        public ITaskItem[] ProjectItemInstanceTaskItemOutputs { get; }

        [Output]
        public ITaskItem[] MixedTaskItemOutputs { get; }

        [Output]
        public ITaskItem[] UtilityTaskItemOutputs { get; }

        public bool Execute() => true;

        public void SetPropertyValue(TaskPropertyInfo property, object value)
        {
        }

        public object GetPropertyValue(TaskPropertyInfo property)
            => property.Name switch
            {
                nameof(ProjectItemInstanceTaskItemOutputs) => ProjectItemInstanceTaskItemOutputs,
                nameof(MixedTaskItemOutputs) => MixedTaskItemOutputs,
                nameof(UtilityTaskItemOutputs) => UtilityTaskItemOutputs,
                _ => throw new ArgumentException($"Unknown property '{property.Name}'.", nameof(property)),
            };
    }

    private sealed class TaskOutputGatheringTaskFactory : ITaskFactory
    {
        private readonly TaskOutputGatheringTask _task;

        public TaskOutputGatheringTaskFactory(ITaskItem[] projectItemInstanceTaskItemOutputs, ITaskItem[] mixedTaskItemOutputs, ITaskItem[] utilityTaskItemOutputs)
        {
            _task = new TaskOutputGatheringTask(projectItemInstanceTaskItemOutputs, mixedTaskItemOutputs, utilityTaskItemOutputs);
        }

        public string FactoryName => nameof(TaskOutputGatheringTaskFactory);

        public Type TaskType => typeof(TaskOutputGatheringTask);

        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost) => true;

        public TaskPropertyInfo[] GetTaskParameters()
        {
            PropertyInfo[] properties = typeof(TaskOutputGatheringTask).GetProperties(BindingFlags.Instance | BindingFlags.Public);
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

        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) => _task;

        public void CleanupTask(ITask task)
        {
        }
    }

    private sealed class StubBuildEngine : IBuildEngine2
    {
        public bool ContinueOnError => false;

        public bool IsRunningMultipleNodes => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => ProjectFile;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => throw new NotImplementedException();

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => throw new NotImplementedException();

        public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) => throw new NotImplementedException();

        public void LogCustomEvent(CustomBuildEventArgs e) => throw new NotImplementedException();

        public void LogErrorEvent(BuildErrorEventArgs e) => throw new NotImplementedException();

        public void LogMessageEvent(BuildMessageEventArgs e) => throw new NotImplementedException();

        public void LogWarningEvent(BuildWarningEventArgs e) => throw new NotImplementedException();
    }
}
