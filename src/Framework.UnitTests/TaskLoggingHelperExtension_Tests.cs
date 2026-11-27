// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;
using SR = Microsoft.Build.Framework.Resources.SR;

namespace Microsoft.Build.UnitTests;

public class TaskLoggingHelperExtensionTests
{
    [Fact]
    public void PrimaryResourcesTakePrecedence()
    {
        MockTask task = new();
        TaskLoggingHelperExtension log = new(task, task.Log.TaskResources, SR.ResourceManager, "MSBuild.");

        log.FormatResourceString("TaskResourceNotFound", "resource", "task").ShouldBe("Primary resource: resource, task");
    }

    [Fact]
    public void MissingPrimaryResourceFallsBackToSharedResources()
    {
        MockTask task = new();
        TaskLoggingHelperExtension log = new(task, task.Log.TaskResources, SR.ResourceManager, "MSBuild.");

        log.FormatResourceString("TaskResourcesNotRegistered", "task").ShouldBe(SR.FormatTaskResourcesNotRegistered("task"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnregisteredResourcesThrowInvalidOperationException(bool primaryResourcesMissing)
    {
        MockTask task = new();
        TaskLoggingHelperExtension log = new(task, task.Log.TaskResources, SR.ResourceManager, "MSBuild.");
        if (primaryResourcesMissing)
        {
            log.TaskResources = null!;
        }
        else
        {
            log.TaskSharedResources = null!;
        }

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => log.FormatResourceString("MyErrorResource", "argument"));

        exception.Message.ShouldBe(SR.FormatTaskResourcesNotRegistered(nameof(MockTask)));
    }

    [Fact]
    public void MissingResourceThrowsArgumentException()
    {
        MockTask task = new();
        TaskLoggingHelperExtension log = new(task, task.Log.TaskResources, SR.ResourceManager, "MSBuild.");

        ArgumentException exception = Should.Throw<ArgumentException>(() => log.FormatResourceString("MissingResource"));

        exception.Message.ShouldBe(SR.FormatTaskResourceNotFound("MissingResource", nameof(MockTask)));
    }
}
