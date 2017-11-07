// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAppThrowingException : TestBase
    {
        [RequiresSpecificFrameworkFact("netcoreapp1.1")]
        public void ItShowsStackTraceWhenRun()
        {
            var root = TestAssets.Get("NonRestoredTestProjects", "AppThrowingException")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var appRoot = Path.Combine(root.FullName, "App");

            new RestoreCommand()
                .WithWorkingDirectory(appRoot)
                .Execute()
                .Should().Pass();

            string msg1 = "Unhandled Exception: AppThrowing.MyException: "
                + "Exception of type 'AppThrowing.MyException' was thrown.";
            string msg2 = "at AppThrowing.MyException.Main(String[] args)";
            new DotnetCommand()
                .WithWorkingDirectory(appRoot)
                .ExecuteWithCapturedOutput("run")
                .Should().Fail()
                         .And.HaveStdErrContaining(msg1)
                         .And.HaveStdErrContaining(msg2);
        }

        [RequiresSpecificFrameworkFact("netcoreapp1.1")]
        public void ItShowsStackTraceWhenRunAsTool()
        {
            var root = TestAssets.Get("NonRestoredTestProjects", "AppThrowingException")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var appRoot = Path.Combine(root.FullName, "App");

            new RestoreCommand()
                .WithWorkingDirectory(appRoot)
                .Execute()
                .Should().Pass();

            new PackCommand()
                .WithWorkingDirectory(appRoot)
                .Execute("-o ../pkgs")
                .Should()
                .Pass();

            var appWithToolDepRoot = Path.Combine(root.FullName, "AppDependingOnOtherAsTool");

            new RestoreCommand()
                .WithWorkingDirectory(appWithToolDepRoot)
                .Execute()
                .Should().Pass();

            string msg1 = "Unhandled Exception: AppThrowing.MyException: "
                + "Exception of type 'AppThrowing.MyException' was thrown.";
            string msg2 = "at AppThrowing.MyException.Main(String[] args)";
            new DotnetCommand()
                .WithWorkingDirectory(appWithToolDepRoot)
                .ExecuteWithCapturedOutput("throwingtool")
                .Should().Fail()
                         .And.HaveStdErrContaining(msg1)
                         .And.HaveStdErrContaining(msg2);
        }
    }
}
