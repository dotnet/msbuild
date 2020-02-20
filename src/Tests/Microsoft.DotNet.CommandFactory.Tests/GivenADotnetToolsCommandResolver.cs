// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Microsoft.DotNet.CommandFactory;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{

    public class GivenADotnetToolsCommandResolver : SdkTest
    {
        private readonly DotnetToolsCommandResolver _dotnetToolsCommandResolver;

        public GivenADotnetToolsCommandResolver(ITestOutputHelper log) : base(log)
        {
            var dotnetToolPath = Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "DotnetTools");
            _dotnetToolsCommandResolver = new DotnetToolsCommandResolver(dotnetToolPath);
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameIsNull()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsNullWhenCommandNameDoesNotExistInProjectTools()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "nonexistent-command",
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void ItReturnsACommandSpec()
        {
            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "dotnet-watch",
            };

            var result = _dotnetToolsCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();

            var commandPath = result.Args.Trim('"');
            commandPath.Should().Contain("dotnet-watch.dll");
        }
    }
}
