// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public sealed class TaskCacheAvailability_Tests(ITestOutputHelper output)
{
    [Fact]
    public void ExplicitCacheModeRequiresAStorageBackend()
    {
        using BuildManager manager = new();
        using TestEnvironment env = TestEnvironment.Create(output);
        if (TaskCacheStore.IsSupported)
        {
            manager.BeginBuild(new BuildParameters { TaskCache = true, BuildCacheDirectory = env.CreateFolder().Path, Loggers = [new MockLogger(output)] });
            manager.EndBuild();
        }
        else
        {
            Should.Throw<ArgumentException>(() => manager.BeginBuild(new BuildParameters { TaskCache = true }))
                .Message.ShouldContain("TaskCache");
        }
    }
}
