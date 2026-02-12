// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class NodeMode_Tests
    {
        [Theory]
        [InlineData(NodeMode.OutOfProcNode, "/nodemode:1")]
        [InlineData(NodeMode.OutOfProcTaskHostNode, "/nodemode:2")]
        [InlineData(NodeMode.OutOfProcRarNode, "/nodemode:3")]
        [InlineData(NodeMode.OutOfProcServerNode, "/nodemode:8")]
        internal void ToCommandLineArgument_ReturnsCorrectFormat(NodeMode nodeMode, string expected)
        {
            string result = NodeModeHelper.ToCommandLineArgument(nodeMode);
            result.ShouldBe(expected);
        }

        [Theory]
        [InlineData("1", NodeMode.OutOfProcNode)]
        [InlineData("2", NodeMode.OutOfProcTaskHostNode)]
        [InlineData("3", NodeMode.OutOfProcRarNode)]
        [InlineData("8", NodeMode.OutOfProcServerNode)]
        internal void TryParse_ParsesIntegerValues(string value, NodeMode expected)
        {
            bool result = NodeModeHelper.TryParse(value, out NodeMode? nodeMode);
            result.ShouldBeTrue();
            nodeMode.ShouldBe(expected);
        }

        [Theory]
        [InlineData("OutOfProcNode", NodeMode.OutOfProcNode)]
        [InlineData("outofprocnode", NodeMode.OutOfProcNode)]
        [InlineData("OUTOFPROCNODE", NodeMode.OutOfProcNode)]
        [InlineData("OutOfProcTaskHostNode", NodeMode.OutOfProcTaskHostNode)]
        [InlineData("OutOfProcRarNode", NodeMode.OutOfProcRarNode)]
        [InlineData("OutOfProcServerNode", NodeMode.OutOfProcServerNode)]
        internal void TryParse_ParsesEnumNames(string value, NodeMode expected)
        {
            bool result = NodeModeHelper.TryParse(value, out NodeMode? nodeMode);
            result.ShouldBeTrue();
            nodeMode.ShouldBe(expected);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("invalid")]
        [InlineData("999")]
        [InlineData("0")]
        [InlineData("-1")]
        public void TryParse_ReturnsFalseForInvalidValues(string value)
        {
            bool result = NodeModeHelper.TryParse(value, out NodeMode? nodeMode);
            result.ShouldBeFalse();
            nodeMode.ShouldBeNull();
        }
    }
}
