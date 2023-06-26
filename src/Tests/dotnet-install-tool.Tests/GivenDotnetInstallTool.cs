// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Install.Tests
{
    public class GivenDotnetInstallTool : SdkTest
    {
        public GivenDotnetInstallTool(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItRunsWithQuietVerbosityByDefault()
        {
            var result = new DotnetToolCommand(Log)
                .Execute("install", "-g", "nonexistent_tool_package");

            result
                .Should()
                .Fail()
                .And
                .NotHaveStdOutContaining("Restoring");
        }

        [Fact]
        public void ItRunsWithTheSpecifiedVerbosity()
        {
            var result = new DotnetToolCommand(Log)
                .Execute("install -g -v:n nonexistent_tool_package".Split());

            result
                .Should()
                .Fail();

            result
                .StdOut
                .Should()
                .ContainVisuallySameFragmentIfNotLocalized("Restoring");
        }
    }
}
