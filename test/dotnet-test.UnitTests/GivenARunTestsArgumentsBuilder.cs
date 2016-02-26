// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.Testing.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenARunTestsArgumentsBuilder
    {
        [Fact]
        public void It_generates_the_right_arguments_for_RunTests()
        {
            const int port = 1;
            const string assembly = "assembly.dll";

            var message = new Message
            {
                Payload = JToken.FromObject(new RunTestsMessage { Tests = new List<string> { "test1", "test2" } })
            };

            var runTestsArgumentsBuilder = new RunTestsArgumentsBuilder(assembly, port, message);

            var arguments = runTestsArgumentsBuilder.BuildArguments();

            arguments.Should().BeEquivalentTo(
                assembly,
                "--designtime",
                "--port",
                $"{port}",
                "--test",
                "test1",
                "--test",
                "test2");
        }
    }
}
