// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
