// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Reflection;

namespace Microsoft.DotNet.Tests
{

    public class GivenADotnetToolsCommandResolver : TestBase
    {
        private readonly DotnetToolsCommandResolver _dotnetToolsCommandResolver;

        // Assets are placed during build of this project
        private static string GetDotnetToolPath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestDotnetToolsLayoutDirectory");

        public GivenADotnetToolsCommandResolver()
        {
            _dotnetToolsCommandResolver = new DotnetToolsCommandResolver(GetDotnetToolPath());
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
