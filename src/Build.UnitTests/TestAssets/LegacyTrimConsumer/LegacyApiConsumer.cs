// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Tasks;

namespace ServicingCompatibility;

public static class LegacyApiConsumer
{
    public static void UseExistingApis(
        BuildParameters parameters,
        BuildSubmission submission,
        ProjectInstance project,
        IEnumerable<ILogger> loggers,
        IEnumerable<ForwardingLoggerRecord> forwardingLoggers)
    {
        using ProjectCollection collection = new(
            new Dictionary<string, string>(),
            loggers,
            forwardingLoggers,
            ToolsetDefinitionLocations.Default,
            1,
            false,
            false);
        using BuildManager manager = new();
        manager.BeginBuild(parameters);
        submission.ExecuteAsync(static _ => { }, new object());
        project.Build(["Build"], loggers, out _);
    }

    public static void UseExistingFactory(IBuildEngine engine)
    {
        ITaskFactory factory = new RoslynCodeTaskFactory();
        factory.Initialize("LegacyTask", new Dictionary<string, TaskPropertyInfo>(), "", engine);
        factory.CreateTask(engine);
    }
}

public sealed class LegacyTaskFactory : ITaskFactory
{
    public string FactoryName => nameof(LegacyTaskFactory);
    public Type TaskType => typeof(LegacyTask);

    public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost) => true;
    public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) => new LegacyTask();
    public TaskPropertyInfo[] GetTaskParameters() => [];
    public void CleanupTask(ITask task) { }
}

public sealed class LegacyTask : ITask
{
    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    public bool Execute() => true;
}
