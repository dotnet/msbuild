// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public class TaskLoader_Tests
{
    [Fact]
    public void TaskAppDomainIsUnloadedWhenItsInitializationFails()
    {
        Type taskType = typeof(SeparateAppDomainTask);
        var loadedType = new LoadedType(taskType, AssemblyLoadInfo.Create(null, taskType.Assembly.Location), taskType.Assembly, typeof(ITaskItem));
        loadedType.HasLoadInSeparateAppDomainAttribute.ShouldBeTrue();

        AppDomain? createdAppDomain = null;
        AppDomain? taskAppDomain = null;
        Should.Throw<InvalidOperationException>(() => TaskLoader.CreateTask(
            loadedType,
            nameof(SeparateAppDomainTask),
            "test.proj",
            taskLine: 1,
            taskColumn: 1,
            logError: (taskLocation, taskLine, taskColumn, message, messageArgs) => { },
            taskEnvironment: null,
            AppDomain.CurrentDomain.SetupInformation,
            appDomainCreated: appDomain =>
            {
                createdAppDomain = appDomain;
                throw new InvalidOperationException("Initialization of the task AppDomain failed.");
            },
            isOutOfProc: false,
            out taskAppDomain));

        createdAppDomain.ShouldNotBeNull();
        taskAppDomain.ShouldBeSameAs(createdAppDomain);
        Should.Throw<AppDomainUnloadedException>(() => createdAppDomain!.FriendlyName);
    }

    [LoadInSeparateAppDomain]
    public class SeparateAppDomainTask : MarshalByRefObject, ITask
    {
        public IBuildEngine? BuildEngine { get; set; }

        public ITaskHost? HostObject { get; set; }

        public bool Execute() => true;
    }
}
#endif
