// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class NodeMode_Tests
{
    [Theory]
    [InlineData("/nodemode:1", 1)]
    [InlineData("-nodemode:1", 1)]
    [InlineData("--nodemode:1", 1)]
    [InlineData("/nmode:1", 1)]
    [InlineData("-nmode:1", 1)]
    [InlineData("--nmode:1", 1)]
    [InlineData("/nodemode:2", 2)]
    [InlineData("/nodemode:3", 3)]
    [InlineData("/nodemode:8", 8)]
    [InlineData("/NODEMODE:2", 2)]
    [InlineData("/NodeMode:8", 8)]
    [InlineData("/nodemode:OutOfProcTaskHostNode", 2)]
    [InlineData("/nodemode:outofprocservernode", 8)]
    [InlineData("\"/nodemode:1\"", 1)]
    [InlineData("/nodemode:\"1\"", 1)]
    [InlineData("/nodemode:1 /nologo", 1)]
    [InlineData("/nologo /nodemode:2", 2)]
    [InlineData("/nologo /nodemode:3 /nodeReuse:true /low:false", 3)]
    [InlineData("  /nodemode:1", 1)]
    [InlineData("/nologo\t/nodemode:1", 1)]
    public void ExtractFromCommandLine_RecognizesNodeModeSwitch(string commandLine, int expectedNodeMode)
    {
        NodeModeHelper.ExtractFromCommandLine(commandLine).ShouldBe((NodeMode)expectedNodeMode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/nologo")]
    [InlineData("/nodemode:")]
    [InlineData("/nodemode:99")]
    [InlineData("/nodemode:1x")]
    [InlineData("/nodemode:InvalidMode")]
    [InlineData("/nodemodes:1")]
    [InlineData("/xnodemode:1")]
    [InlineData("x/nodemode:1")]
    [InlineData("//nodemode:1")]
    [InlineData("---nodemode:1")]
    [InlineData("nodemode:1")]
    [InlineData("/p:A=/nodemode:1")]
    [InlineData("/p:Path=C:\\nodemode:1")]
    public void ExtractFromCommandLine_IgnoresTextThatIsNotTheSwitch(string? commandLine)
    {
        NodeModeHelper.ExtractFromCommandLine(commandLine!).ShouldBeNull();
    }

    [Fact]
    public void ExtractFromCommandLine_RecognizesTheArgumentsMSBuildUsesToLaunchNodes()
    {
        foreach (NodeMode nodeMode in new[] { NodeMode.OutOfProcNode, NodeMode.OutOfProcTaskHostNode, NodeMode.OutOfProcRarNode, NodeMode.OutOfProcServerNode })
        {
            NodeModeHelper.ExtractFromCommandLine(NodeModeHelper.ToCommandLineArgument(nodeMode)).ShouldBe(nodeMode);
            NodeModeHelper.ExtractFromCommandLine($"/nologo {NodeModeHelper.ToCommandLineArgument(nodeMode)} /nodeReuse:true").ShouldBe(nodeMode);
        }
    }
}
