// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class OutOfProcTaskAppDomainWrapper_Tests : IDisposable
    {
        private readonly OutOfProcTaskAppDomainWrapper _wrapper = new();

        public void Dispose() => _wrapper.CleanupTask();

        /// <summary>
        /// When the requested task type cannot be found in the assembly, the underlying
        /// <c>TypeLoader.Load</c> returns <see langword="null"/> rather than throwing. In that case
        /// ExecuteTask must report a graceful initialization failure instead of crashing with a
        /// <see cref="System.NullReferenceException"/>.
        /// </summary>
        [Fact]
        public void ExecuteTaskReturnsInitializationFailureWhenTaskTypeNotFound()
        {
            OutOfProcTaskHostTaskResult result = _wrapper.ExecuteTask(
                oopTaskHostNode: null,
                taskName: "ThisTaskTypeDoesNotExistInTheAssembly",
                taskLocation: typeof(OutOfProcTaskAppDomainWrapper_Tests).Assembly.Location,
                taskFile: "test.proj",
                taskLine: 1,
                taskColumn: 1,
                targetName: "TestTarget",
                projectFile: "test.proj",
#if FEATURE_APPDOMAIN
                appDomainSetup: null,
#endif
                hostServices: null,
                taskParams: new Dictionary<string, TaskParameter>());

            result.ShouldNotBeNull();
            result.Result.ShouldBe(TaskCompleteType.CrashedDuringInitialization);
            result.ExceptionMessage.ShouldBe("TaskInstantiationFailureError");
            result.TaskException.ShouldNotBeNull();
        }

        [Theory]
        [InlineData(typeof(TaskWithoutEnvironmentInitializer))]
        [InlineData(typeof(TaskWithEnvironmentConstructor))]
#if FEATURE_APPDOMAIN
        [InlineData(typeof(TaskInSeparateAppDomain))]
#endif
        public void ExecuteTaskSetsFallbackEnvironmentBeforeParametersAndExecution(Type taskType)
        {
            OutOfProcTaskHostTaskResult result = _wrapper.ExecuteTask(
                oopTaskHostNode: null,
                taskName: taskType.FullName,
                taskLocation: taskType.Assembly.Location,
                taskFile: "test.proj",
                taskLine: 1,
                taskColumn: 1,
                targetName: "TestTarget",
                projectFile: "test.proj",
#if FEATURE_APPDOMAIN
                appDomainSetup: AppDomain.CurrentDomain.SetupInformation,
#endif
                hostServices: null,
                taskParams: new Dictionary<string, TaskParameter>
                {
                    [nameof(TaskWithoutEnvironmentInitializer.InputPath)] = new TaskParameter("input.txt")
                });

            result.TaskException.ShouldBeNull();
            result.Result.ShouldBe(TaskCompleteType.Success);
            result.FinalParameterValues[nameof(TaskWithoutEnvironmentInitializer.ParameterEnvironmentIsFallback)].ShouldBe(true);
            result.FinalParameterValues[nameof(TaskWithoutEnvironmentInitializer.ExecutionEnvironmentIsFallback)].ShouldBe(true);

            if (taskType == typeof(TaskWithEnvironmentConstructor))
            {
                result.FinalParameterValues[nameof(TaskWithEnvironmentConstructor.ConstructorEnvironmentMatchesProperty)].ShouldBe(true);
            }
        }

        public class TaskWithoutEnvironmentInitializer : MarshalByRefObject, IMultiThreadableTask
        {
            private string _inputPath;

            public IBuildEngine BuildEngine { get; set; }

            public ITaskHost HostObject { get; set; }

            public TaskEnvironment TaskEnvironment { get; set; }

            [Output]
            public bool ParameterEnvironmentIsFallback { get; private set; }

            [Output]
            public bool ExecutionEnvironmentIsFallback { get; private set; }

            public string InputPath
            {
                get => _inputPath;
                set
                {
                    _inputPath = value;
                    ParameterEnvironmentIsFallback = ReferenceEquals(TaskEnvironment, TaskEnvironment.Fallback);
                }
            }

            public bool Execute()
            {
                ExecutionEnvironmentIsFallback = ReferenceEquals(TaskEnvironment, TaskEnvironment.Fallback);
                return true;
            }
        }

        public sealed class TaskWithEnvironmentConstructor : TaskWithoutEnvironmentInitializer
        {
            private readonly TaskEnvironment _constructorEnvironment;

            public TaskWithEnvironmentConstructor(TaskEnvironment taskEnvironment)
            {
                _constructorEnvironment = taskEnvironment;
                TaskEnvironment = taskEnvironment;
            }

            [Output]
            public bool ConstructorEnvironmentMatchesProperty => ReferenceEquals(_constructorEnvironment, TaskEnvironment);
        }

#if FEATURE_APPDOMAIN
        [LoadInSeparateAppDomain]
        public sealed class TaskInSeparateAppDomain : TaskWithoutEnvironmentInitializer
        {
        }
#endif

#if NET
        [Fact]
        public void TypedTaskItemParseFailureUsesParameterConversionDiagnostic()
        {
            ITaskItem item = new TaskItem("not-an-int");

            System.Exception exception = Should.Throw<System.Exception>(
                () => OutOfProcTaskAppDomainWrapperBase.ConvertTaskParameterValue(item, typeof(ITaskItem<int>)));

            exception.GetType().Name.ShouldBe("TaskParameterConversionException");
            exception.InnerException.ShouldBeOfType<System.ArgumentException>();
        }
#endif
    }
}
