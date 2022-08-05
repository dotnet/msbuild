// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public class DotnetNew3CompleteTests : SdkTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNew3CompleteTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public Task CanDoTabCompletion()
        {
            string homeDir = TestUtils.CreateTemporaryFolder();
            var command = new DotnetNewCommand(_log, "complete", $"new3 --debug:custom-hive {homeDir} ");
            // Replace command "new" with "dotnet-new3.dll new3"
			string dotnetNew3AssemblyPath = typeof(Dotnet_new3.Program).Assembly.Location;
            command.Arguments.RemoveAt(0);
            command.Arguments.InsertRange(0, new List<string> { dotnetNew3AssemblyPath, "new3" });
            string dotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            string templatesLocation = Path.Combine(dotnetRoot, "templates");
            var packagePaths = Directory.EnumerateFiles(templatesLocation, "Microsoft.DotNet.Common.ItemTemplates.*.nupkg", SearchOption.AllDirectories);
            string packageLocation = packagePaths.FirstOrDefault();
            Environment.SetEnvironmentVariable("DN3", Path.GetDirectoryName(packageLocation));
            var commandResult = command.WithoutCustomHive().Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut).UniqueForOSPlatform();
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/command-line-api/issues/1519")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanDoTabCompletionAtGivenPosition()
        {
            string homeDir = TestUtils.CreateTemporaryFolder();
            var commandResult = new DotnetNewCommand(_log, "complete", $"new3 co --debug:custom-hive {homeDir} --language C#", "--position", "7")
                .WithoutCustomHive()
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOut("console");
        }
    }
}
