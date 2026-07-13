// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
/// Compares the two mechanisms MSBuild can use to construct a task instance in-proc, so the constructor-based
/// path that <see cref="LoadedType.CreateInstance"/> uses can be measured directly against
/// <see cref="Activator"/> — with no <see cref="TaskLoader"/> or engine plumbing in the loop.
///
/// Four cases are measured across two axes (mechanism × constructor shape):
/// <list type="bullet">
///   <item><description><c>Activator_Parameterless</c> — <see cref="Activator.CreateInstance(Type)"/> on a task
///     that only exposes a parameterless constructor (the CLR's cached per-type activator; the baseline).</description></item>
///   <item><description><c>Activator_TaskEnvironmentConstructor</c> — <see cref="Activator.CreateInstance(Type, object[])"/>
///     on a task that declares a <see cref="TaskEnvironment"/> constructor (allocates a one-element argument
///     array and pays the reflection-binder overload resolution on every call).</description></item>
///   <item><description><c>LoadedType_Parameterless</c> — <see cref="LoadedType.CreateInstance"/> on the same
///     parameterless task; invokes the cached constructor directly.</description></item>
///   <item><description><c>LoadedType_TaskEnvironmentConstructor</c> — <see cref="LoadedType.CreateInstance"/> on the
///     same TaskEnvironment task; invokes the cached constructor directly with the environment.</description></item>
/// </list>
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

        // Sanity-check that the two benchmark task types actually exercise the two intended paths;
        // otherwise the benchmark would silently measure the same thing twice.
        if (HasTaskEnvironmentConstructor(_parameterlessType) || !HasTaskEnvironmentConstructor(_taskEnvironmentType))
        {
            throw new InvalidOperationException("Benchmark task types do not declare the expected constructors.");
        }
    }

    private static bool HasTaskEnvironmentConstructor(Type type)
    {
        foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
        {
            ParameterInfo[] parameters = constructor.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(TaskEnvironment))
            {
                return true;
            }
        }

        return false;
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
    public ITask? LoadedType_Parameterless()
        => _parameterlessLoadedType.CreateInstance(_taskEnvironment);

    [Benchmark]
    public ITask? LoadedType_TaskEnvironmentConstructor()
        => _taskEnvironmentLoadedType.CreateInstance(_taskEnvironment);

    /// <summary>
    /// Task exposing only the implicit parameterless constructor. Exercises the parameterless
    /// instantiation path (no <see cref="TaskEnvironment"/> constructor).
    /// </summary>
    private sealed class ParameterlessBenchmarkTask : ITask
    {
        public IBuildEngine BuildEngine { get; set; } = null!;

        public ITaskHost HostObject { get; set; } = null!;

        public bool Execute() => true;
    }

    /// <summary>
    /// Task declaring a <see cref="TaskEnvironment"/> constructor. Exercises the constructor-injection
    /// instantiation path.
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
