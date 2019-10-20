// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.CommandFactory;
using Xunit;

namespace Microsoft.DotNet.Tests
{
    public class GivenARootedCommandResolver
    {
        [Fact]
        public void It_returns_null_when_CommandName_is_null()
        {
            var rootedCommandResolver = new RootedCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = null,
                CommandArguments = null
            };

            var result = rootedCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_null_when_CommandName_is_not_rooted()
        {
            var rootedCommandResolver = new RootedCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "some/relative/path",
                CommandArguments = null
            };

            var result = rootedCommandResolver.Resolve(commandResolverArguments);

            result.Should().BeNull();
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_CommandName_as_Path_when_CommandName_is_rooted()
        {
            var rootedCommandResolver = new RootedCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "/some/rooted/path",
                CommandArguments = null
            };

            var result = rootedCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Path.Should().Be(commandResolverArguments.CommandName);
        }

        [Fact]
        public void It_escapes_CommandArguments_when_returning_a_CommandSpec()
        {
            var rootedCommandResolver = new RootedCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "/some/rooted/path",
                CommandArguments = new[] { "arg with space" }
            };

            var result = rootedCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Path.Should().Be(commandResolverArguments.CommandName);

            result.Args.Should().Be("\"arg with space\"");
        }

        [Fact]
        public void It_returns_a_CommandSpec_with_Args_as_stringEmpty_when_returning_a_CommandSpec_and_CommandArguments_are_null()
        {
            var rootedCommandResolver = new RootedCommandResolver();

            var commandResolverArguments = new CommandResolverArguments()
            {
                CommandName = "/some/rooted/path",
                CommandArguments = null
            };

            var result = rootedCommandResolver.Resolve(commandResolverArguments);

            result.Should().NotBeNull();
            result.Path.Should().Be(commandResolverArguments.CommandName);

            result.Args.Should().Be(string.Empty);
        }
    }
}
