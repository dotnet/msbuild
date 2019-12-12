// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAppThrowingException : SdkTest
    {
        public GivenAppThrowingException(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresSpecificFrameworkFact("netcoreapp1.1")]
        public void ItShowsStackTraceWhenRun()
        {
            var root = _testAssetsManager.CopyTestAsset("AppThrowingException", testAssetSubdirectory: TestAssetSubdirectories.NonRestoredTestProjects)
                .WithSource()
                .Path;

            var appRoot = Path.Combine(root, "App");

            string msg1 = "Unhandled Exception: AppThrowing.MyException: "
                + "Exception of type 'AppThrowing.MyException' was thrown.";
            string msg2 = "at AppThrowing.MyException.Main(String[] args)";
            new DotnetCommand(Log)
                .WithWorkingDirectory(appRoot)
                .Execute("run")
                .Should().Fail()
                         .And.HaveStdErrContaining(msg1)
                         .And.HaveStdErrContaining(msg2);
        }

        [RequiresSpecificFrameworkFact("netcoreapp1.1")]
        public void ItShowsStackTraceWhenRunAsTool()
        {
            var root = _testAssetsManager.CopyTestAsset("AppThrowingException", testAssetSubdirectory: TestAssetSubdirectories.NonRestoredTestProjects)
                .WithSource()
                .Path;

            var appRoot = Path.Combine(root, "App");

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(appRoot)
                .Execute("-o", "../pkgs")
                .Should()
                .Pass();

            var appWithToolDepRoot = Path.Combine(root, "AppDependingOnOtherAsTool");

            new RestoreCommand(Log, appWithToolDepRoot)
                .Execute()
                .Should().Pass();

            string msg1 = "Unhandled Exception: AppThrowing.MyException: "
                + "Exception of type 'AppThrowing.MyException' was thrown.";
            string msg2 = "at AppThrowing.MyException.Main(String[] args)";
            new DotnetCommand(Log)
                .WithWorkingDirectory(appWithToolDepRoot)
                .Execute("throwingtool")
                .Should().Fail()
                         .And.HaveStdErrContaining(msg1)
                         .And.HaveStdErrContaining(msg2);
        }
    }
}
