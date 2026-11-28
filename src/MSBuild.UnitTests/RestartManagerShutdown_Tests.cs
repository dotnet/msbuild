// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

/// <summary>
/// Tests for the external-stop (Windows Restart Manager / console close) shutdown path used by
/// out-of-proc worker nodes. Runs that depend on a real console control event are covered by the
/// manual validation described in the PR, not here.
/// </summary>
public class RestartManagerShutdown_Tests
{
    private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;

    [Fact]
    public void OutOfProcNode_RequestExternalShutdown_WithNoActiveNode_IsNoOp()
    {
        // No node is inside Run(), so there is nothing to shut down. Must not throw.
        Should.NotThrow(() => OutOfProcNode.RequestExternalShutdown());
    }

    [Fact]
    public void OutOfProcNode_RequestExternalShutdown_CalledRepeatedly_IsNoOp()
    {
        Should.NotThrow(() =>
        {
            OutOfProcNode.RequestExternalShutdown();
            OutOfProcNode.RequestExternalShutdown();
        });
    }

    [Fact]
    public void OutOfProcTaskHostNode_RequestExternalShutdown_WithNoActiveInstance_IsNoOp()
    {
        // Mirrors the node case for the task host. Must not throw when no task host is running.
        Should.NotThrow(() => OutOfProcTaskHostNode.RequestExternalShutdown());
    }

    [Fact]
    public void EnsureWindowsConsoleShutdownHandlersRegistered_CalledTwice_ReusesSameHandlerDelegate()
    {
        if (!NativeMethodsShared.IsWindows)
        {
            return;
        }

        MethodInfo ensure = typeof(MSBuildApp).GetMethod("EnsureWindowsConsoleShutdownHandlersRegistered", StaticNonPublic)
            ?? throw new InvalidOperationException("EnsureWindowsConsoleShutdownHandlersRegistered is missing from MSBuildApp");
        FieldInfo handlerField = typeof(MSBuildApp).GetField("s_consoleCtrlHandler", StaticNonPublic)
            ?? throw new InvalidOperationException("s_consoleCtrlHandler is missing from MSBuildApp");
        MethodInfo setHandler = typeof(MSBuildApp).GetMethod("SetConsoleCtrlHandler", StaticNonPublic)
            ?? throw new InvalidOperationException("SetConsoleCtrlHandler is missing from MSBuildApp");

        try
        {
            ensure.Invoke(null, null);
            object? first = handlerField.GetValue(null);

            ensure.Invoke(null, null);
            object? second = handlerField.GetValue(null);

            // Each call to ensure creates a new delegate and overwrites the field. The first delegate is
            // still registered with the OS but no longer referenced, so the GC can collect it and the
            // next Ctrl event crashes the process. The handler should be registered exactly once.
            second.ShouldBeSameAs(first);
        }
        finally
        {
            // Unregister so this process does not keep a console handler that cancels the build.
            object? registered = handlerField.GetValue(null);
            if (registered is not null)
            {
                setHandler.Invoke(null, new[] { registered, (object)false });
            }
        }
    }
}
