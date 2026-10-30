// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd;

/// <summary>
/// Tests for the pipe check <see cref="NodeProviderOutOfProcBase.ShutdownAllNodes"/> uses to skip processes that are not nodes.
/// </summary>
public class NodePipeDiscovery_Tests
{
    [Fact]
    public void HasNodePipe_DetectsOnlyProcessesWithANodePipe()
    {
        // The pipe name is all that is checked, so a pid that no real process uses keeps this independent of running nodes.
        int pidWithPipe = int.MaxValue - new Random().Next(1_000_000);
        int pidWithoutPipe = pidWithPipe - 1;

        using NamedPipeServerStream pipe = new(
            NamedPipeUtil.GetPlatformSpecificPipeName(pidWithPipe),
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        HashSet<string> pipeNames = NodeProviderOutOfProcBase.TryGetExistingPipeNames();

        if (NativeMethodsShared.IsWindows)
        {
            pipeNames.ShouldNotBeNull("Listing named pipes should work on Windows; otherwise every candidate is contacted.");
        }

        NodeProviderOutOfProcBase.HasNodePipe(pidWithPipe, pipeNames).ShouldBeTrue();
        NodeProviderOutOfProcBase.HasNodePipe(pidWithoutPipe, pipeNames).ShouldBeFalse();
    }
}
