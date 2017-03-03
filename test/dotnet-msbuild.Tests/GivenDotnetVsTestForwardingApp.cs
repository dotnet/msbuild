// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.VSTest;
using FluentAssertions;
using Xunit;
using System;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetVsTestForwardingApp
    {
        [Fact]
        public void ItRunsVsTestApp()
        {
            new VSTestForwardingApp(new string[0])
                .GetProcessStartInfo().Arguments.Should().EndWith("vstest.console.dll");
        }
    }
}
