// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class OutOfProcTaskAppDomainWrapper_Tests
    {
        /// <summary>
        /// When the requested task type cannot be found in the assembly, the underlying
        /// <c>TypeLoader.Load</c> returns <see langword="null"/> rather than throwing. In that case
        /// ExecuteTask must report a graceful initialization failure instead of crashing with a
        /// <see cref="System.NullReferenceException"/>.
        /// </summary>
        [Fact]
        public void ExecuteTaskReturnsInitializationFailureWhenTaskTypeNotFound()
        {
            OutOfProcTaskAppDomainWrapper wrapper = new();

            OutOfProcTaskHostTaskResult result = wrapper.ExecuteTask(
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

#if NET
        [Fact]
        public void ConvertTaskParameterValueBindsSerializedValues()
        {
            OutOfProcTaskAppDomainWrapperBase.ConvertTaskParameterValue("Deep", typeof(TestMode))
                .ShouldBe(TestMode.Deep);

            OutOfProcTaskAppDomainWrapperBase.ConvertTaskParameterValue(
                    new[] { "Shallow", "Deep" },
                    typeof(TestMode[]))
                .ShouldBeOfType<TestMode[]>()
                .ShouldBe([TestMode.Shallow, TestMode.Deep]);

            string firstPath = Path.GetFullPath("first.txt");
            string secondPath = Path.GetFullPath("second.txt");
            FileInfo[] files = OutOfProcTaskAppDomainWrapperBase.ConvertTaskParameterValue(
                    new[] { firstPath, secondPath },
                    typeof(FileInfo[]))
                .ShouldBeOfType<FileInfo[]>();

            files[0].FullName.ShouldBe(firstPath);
            files[1].FullName.ShouldBe(secondPath);
        }

        private enum TestMode
        {
            Shallow,
            Deep,
        }
#endif
    }
}
