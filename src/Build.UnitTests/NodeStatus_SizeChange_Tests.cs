// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Threading.Tasks;

using Microsoft.Build.Logging;
using VerifyMSTest;
using System.Runtime.CompilerServices;


namespace Microsoft.Build.CommandLine.UnitTests;

[UseInvariantCulture]
[TestClass]
[UsesVerify]
public partial class NodeStatus_SizeChange_Tests
{
    [ModuleInitializer]
    internal static void InitializeVerify() => Verifier.UseProjectRelativeDirectory("Snapshots");

    private readonly TerminalNodeStatus _status = new("Namespace.Project", "TargetFramework", null, "Target", new MockStopwatch());

    [MSBuildTestMethod]
    public async Task EverythingFits()
    {
        TerminalNodesFrame frame = new([_status], width: 80, height: 5);

        await Verifier.Verify(frame.RenderNodeStatus(0).ToString());
    }

    [MSBuildTestMethod]
    public async Task TargetIsTruncatedFirst()
    {
        TerminalNodesFrame frame = new([_status], width: 45, height: 5);

        await Verifier.Verify(frame.RenderNodeStatus(0).ToString());
    }

    [MSBuildTestMethod]
    public async Task NamespaceIsTruncatedNext()
    {
        TerminalNodesFrame frame = new([_status], width: 40, height: 5);

        await Verifier.Verify(frame.RenderNodeStatus(0).ToString());
    }

    [MSBuildTestMethod]
    public async Task GoesToProject()
    {
        TerminalNodesFrame frame = new([_status], width: 10, height: 5);

        await Verifier.Verify(frame.RenderNodeStatus(0).ToString());
    }
}
