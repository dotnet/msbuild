// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class GivenParserDirectives : SdkTest
    {
        public GivenParserDirectives(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItCanInvokeParseDirective()
        {
            string [] args = new[] { "[parse]", "build", "-o", "output" };
            new DotnetCommand(Log, args)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("[ dotnet [ build [ -o <output> ] ] ]");
        }

        [Fact]
        public void ItCanInvokeSuggestDirective()
        {
            string[] args = new[] { "[suggest]", "--l"};
            new DotnetCommand(Log, args)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("--list-runtimes")
                .And
                .HaveStdOutContaining("--list-sdks");
        }

        [Fact]
        public void ItCanAcceptResponseFiles()
        {
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "response.rsp"), "build");
            string[] args = new[] { @"@response.rsp", "-h" };
            new DotnetCommand(Log, args)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(@"dotnet build [<PROJECT | SOLUTION>...] [options]");
        }
    }
}
