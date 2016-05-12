// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Tools.Run.Tests
{
    public class GivenARunCommand : TestBase
    {
        private const int RunExitCode = 29;

        [Fact]
        public void ItDoesntRedirectStandardOutAndError()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppSimple")
                                         .WithLockFiles();

            new BuildCommand(instance.TestRoot)
                .Execute()
                .Should()
                .Pass();

            RunCommand runCommand = new RunCommand(new FailOnRedirectOutputCommandFactory());
            runCommand.Project = instance.TestRoot;

            runCommand.Start()
                .Should()
                .Be(RunExitCode);
        }

        private class FailOnRedirectOutputCommandFactory : ICommandFactory
        {
            public ICommand Create(string commandName, IEnumerable<string> args, NuGetFramework framework = null, string configuration = "Debug")
            {
                return new FailOnRedirectOutputCommand();
            }

            /// <summary>
            /// A Command that will fail if a caller tries redirecting StdOut or StdErr.
            /// </summary>
            private class FailOnRedirectOutputCommand : ICommand
            {
                public CommandResult Execute()
                {
                    return new CommandResult(null, RunExitCode, null, null);
                }

                public ICommand CaptureStdErr()
                {
                    throw new NotSupportedException();
                }

                public ICommand CaptureStdOut()
                {
                    throw new NotSupportedException();
                }

                public ICommand ForwardStdErr(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true)
                {
                    throw new NotSupportedException();
                }

                public ICommand ForwardStdOut(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true)
                {
                    throw new NotSupportedException();
                }

                public ICommand OnErrorLine(Action<string> handler)
                {
                    throw new NotSupportedException();
                }

                public ICommand OnOutputLine(Action<string> handler)
                {
                    throw new NotSupportedException();
                }

                public string CommandArgs
                {
                    get
                    {
                        throw new NotImplementedException();
                    }
                }

                public string CommandName
                {
                    get
                    {
                        throw new NotImplementedException();
                    }
                }

                public CommandResolutionStrategy ResolutionStrategy
                {
                    get
                    {
                        throw new NotImplementedException();
                    }
                }

                public ICommand EnvironmentVariable(string name, string value)
                {
                    throw new NotImplementedException();
                }

                public ICommand WorkingDirectory(string projectDirectory)
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
