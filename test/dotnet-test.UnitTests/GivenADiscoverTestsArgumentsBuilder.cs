// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenADiscoverTestsArgumentsBuilder
    {
        [Fact]
        public void It_generates_the_right_arguments_for_DiscoverTests()
        {
            const int port = 1;
            const string assembly = "assembly.dll";

            var discoverTestsArgumentsBuilder = new DiscoverTestsArgumentsBuilder(assembly, port);

            var arguments = discoverTestsArgumentsBuilder.BuildArguments();

            arguments.Should().BeEquivalentTo(assembly, "--list", "--designtime", "--port", $"{port}");
        }
    }
}
