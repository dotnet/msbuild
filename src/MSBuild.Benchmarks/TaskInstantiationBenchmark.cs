// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks the cost of creating a task instance through the engine's <see cref="TaskLoader.CreateTask"/>
/// path, so the newly-added TaskEnvironment constructor-injection mechanism can be compared against the
/// pre-existing parameterless path and against a raw <see cref="Activator.CreateInstance(Type)"/> call.
///
/// Three axes are measured:
/// <list type="bullet">
///   <item><description><c>Activator_Parameterless</c> — a raw reflection <see cref="Activator.CreateInstance(Type)"/>
///     call with no engine involvement (the floor cost of reflective instantiation).</description></item>
///   <item><description><c>TaskLoader_Parameterless</c> — the current engine mechanism on a task type that only
///     exposes a parameterless constructor (the <see cref="LoadedType.HasTaskEnvironmentConstructor"/> == false path).</description></item>
///   <item><description><c>TaskLoader_TaskEnvironmentConstructor</c> — the current engine mechanism on a task type that
///     declares a <see cref="TaskEnvironment"/> constructor (the injection path, which allocates a one-element
///     argument array and dispatches to the parameterized <see cref="Activator.CreateInstance(Type, object[])"/>).</description></item>
/// </list>
///
/// <c>Activator_TaskEnvironmentConstructor</c> is included as an additional reference point that isolates the
/// raw parameterized-<see cref="Activator"/> cost (argument-array allocation + constructor match) from any engine
/// overhead in the <c>TaskLoader_*</c> variants.
/// </summary>
[MemoryDiagnoser]
public class TaskInstantiationBenchmark
{
    private Type _parameterlessType = null!;
    private Type _taskEnvironmentType = null!;

    private LoadedType _parameterlessLoadedType = null!;
    private LoadedType _taskEnvironmentLoadedType = null!;

    private TaskEnvironment _taskEnvironment = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _parameterlessType = typeof(ParameterlessBenchmarkTask);
        _taskEnvironmentType = typeof(TaskEnvironmentBenchmarkTask);

        _taskEnvironment = TaskEnvironment.Fallback;

        _parameterlessLoadedType = CreateLoadedType(_parameterlessType);
        _taskEnvironmentLoadedType = CreateLoadedType(_taskEnvironmentType);

        // Sanity-check that the two LoadedType instances actually exercise the two intended paths;
        // otherwise the benchmark would silently measure the same thing twice.
        if (_parameterlessLoadedType.HasTaskEnvironmentConstructor || !_taskEnvironmentLoadedType.HasTaskEnvironmentConstructor)
        {
            throw new InvalidOperationException("Benchmark task types are not detected as expected by LoadedType.");
        }
    }

    private static LoadedType CreateLoadedType(Type type)
    {
        AssemblyLoadInfo loadInfo = AssemblyLoadInfo.Create(type.FullName, null);
        return new LoadedType(type, loadInfo, type.Assembly, typeof(ITaskItem));
    }

    [Benchmark(Baseline = true)]
    public ITask Activator_Parameterless()
        => (ITask)Activator.CreateInstance(_parameterlessType)!;

    [Benchmark]
    public ITask Activator_TaskEnvironmentConstructor()
        => (ITask)Activator.CreateInstance(_taskEnvironmentType, new object[] { _taskEnvironment })!;

    [Benchmark]
    public ITask? TaskLoader_Parameterless()
        => CreateTask(_parameterlessLoadedType);

    [Benchmark]
    public ITask? TaskLoader_TaskEnvironmentConstructor()
        => CreateTask(_taskEnvironmentLoadedType);

    private ITask? CreateTask(LoadedType loadedType)
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        => TaskLoader.CreateTask(
            loadedType,
            loadedType.Type.Name,
            "task-instantiation-benchmark.proj",
            1,
            1,
            static (_, _, _, _, _) => { },
            _taskEnvironment,
#if FEATURE_APPDOMAIN
            AppDomain.CurrentDomain.SetupInformation,
            static _ => { },
#endif
            isOutOfProc: false
#if FEATURE_APPDOMAIN
            , out _
#endif
            );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter

    /// <summary>
    /// Task exposing only the implicit parameterless constructor. Exercises the
    /// <see cref="LoadedType.HasTaskEnvironmentConstructor"/> == false instantiation path.
    /// </summary>
    private sealed class ParameterlessBenchmarkTask : ITask
    {
        public IBuildEngine BuildEngine { get; set; } = null!;

        public ITaskHost HostObject { get; set; } = null!;

        public bool Execute() => true;
    }

    /// <summary>
    /// Task declaring a <see cref="TaskEnvironment"/> constructor. Exercises the constructor-injection
    /// instantiation path (parameterized <see cref="Activator.CreateInstance(Type, object[])"/>).
    /// </summary>
    private sealed class TaskEnvironmentBenchmarkTask : ITask, IMultiThreadableTask
    {
        public TaskEnvironmentBenchmarkTask(TaskEnvironment taskEnvironment) => TaskEnvironment = taskEnvironment;

        public TaskEnvironment TaskEnvironment { get; set; }

        public IBuildEngine BuildEngine { get; set; } = null!;

        public ITaskHost HostObject { get; set; } = null!;

        public bool Execute() => true;
    }
}
