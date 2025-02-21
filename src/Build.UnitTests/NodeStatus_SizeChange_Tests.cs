// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Logging;

using VerifyTests;
using VerifyXunit;
using Xunit;

using static VerifyXunit.Verifier;


namespace Microsoft.Build.CommandLine.UnitTests;

[UsesVerify]
public class NodeStatus_SizeChange_Tests : IDisposable
{
    private readonly TerminalNodeStatus _status = new("Namespace.Project", "TargetFramework", "Target", new MockStopwatch());
    private CultureInfo _currentCulture;

    public NodeStatus_SizeChange_Tests()
    {
        _currentCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        UseProjectRelativeDirectory("Snapshots");
    }

    [Fact]
    public async Task EverythingFits()
    {
        TerminalNodesFrame frame = new([_status], width: 80, height: 5);

        await Verify(frame.RenderNodeStatus(0).ToString());
    }

    [Fact]
    public async Task TargetIsTruncatedFirst()
    {
        TerminalNodesFrame frame = new([_status], width: 45, height: 5);

        await Verify(frame.RenderNodeStatus(0).ToString());
    }

    [Fact]
    public async Task NamespaceIsTruncatedNext()
    {
        TerminalNodesFrame frame = new([_status], width: 40, height: 5);

        await Verify(frame.RenderNodeStatus(0).ToString());
    }

    [Fact]
    public async Task GoesToProject()
    {
        TerminalNodesFrame frame = new([_status], width: 10, height: 5);

        await Verify(frame.RenderNodeStatus(0).ToString());
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _currentCulture;
    }
}
