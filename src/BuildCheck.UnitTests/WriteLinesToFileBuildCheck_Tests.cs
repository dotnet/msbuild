// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Checks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public sealed class WriteLinesToFileBuildCheck_Tests
{
    private readonly WriteLinesToFileBuildCheck _check;

    private readonly MockBuildCheckRegistrationContext _registrationContext = new();

    public WriteLinesToFileBuildCheck_Tests()
    {
        _check = new WriteLinesToFileBuildCheck();
        _check.RegisterActions(_registrationContext);
    }

    [Theory]
    [InlineData("WriteLinesToFile")]
    [InlineData("writelinestofile")]
    public void WriteLinesToFileTask_WithoutOverwrite_ShouldShowWarning(string taskName)
    {
        _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData(taskName, []));

        _registrationContext.Results.Count.ShouldBe(1);
        _registrationContext.Results[0].CheckRule.Id.ShouldBe("BC0303");
    }

    [Theory]
    [InlineData("Overwrite", true)]
    [InlineData("Overwrite", false)]
    [InlineData("overwrite", true)]
    [InlineData("OVERWRITE", false)]
    public void WriteLinesToFileTask_WithExplicitOverwrite_ShouldNotShowWarning(string parameterName, bool overwrite)
    {
        _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData(
            "WriteLinesToFile",
            new Dictionary<string, TaskInvocationCheckData.TaskParameter>
            {
                { parameterName, new TaskInvocationCheckData.TaskParameter(overwrite, IsOutput: false) },
            }));

        _registrationContext.Results.Count.ShouldBe(0);
    }

    [Fact]
    public void DifferentTask_WithoutOverwrite_ShouldNotShowWarning()
    {
        _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Message", []));

        _registrationContext.Results.Count.ShouldBe(0);
    }

    private static TaskInvocationCheckData MakeTaskInvocationData(
        string taskName,
        Dictionary<string, TaskInvocationCheckData.TaskParameter> parameters)
    {
        string projectFile = Framework.NativeMethods.IsWindows ? @"C:\fake\project.proj" : "/fake/project.proj";
        return new TaskInvocationCheckData(
            projectFile,
            null,
            Construction.ElementLocation.EmptyLocation,
            taskName,
            projectFile,
            parameters);
    }
}
