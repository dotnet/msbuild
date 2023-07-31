// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.CommandFactory;

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
