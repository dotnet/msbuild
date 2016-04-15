// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils
{
    public class BuiltInCommandTests : TestBase
    {
        /// <summary>
        /// Tests that BuiltInCommand.Execute returns the correct exit code and a
        /// valid StartInfo FileName and Arguments.
        /// </summary>
        [Fact]
        public void TestExecute()
        {
            Func<string[], int> testCommand = args => args.Length;
            string[] testCommandArgs = new[] { "1", "2" };

            var builtInCommand = new BuiltInCommand("fakeCommand", testCommandArgs, testCommand);
            CommandResult result = builtInCommand.Execute();

            Assert.Equal(testCommandArgs.Length, result.ExitCode);
            Assert.Equal(new Muxer().MuxerPath, result.StartInfo.FileName);
            Assert.Equal("fakeCommand 1 2", result.StartInfo.Arguments);
        }

        /// <summary>
        /// Tests that BuiltInCommand.Execute raises the OnOutputLine and OnErrorLine
        /// the correct number of times and with the correct content.
        /// </summary>
        [Fact]
        public void TestOnOutputLines()
        {
            int exitCode = 29;

            Func<string[], int> testCommand = args =>
            {
                Console.Out.Write("first");
                Console.Out.WriteLine("second");
                Console.Out.WriteLine("third");

                Console.Error.WriteLine("fourth");
                Console.Error.WriteLine("fifth");

                return exitCode;
            };

            int onOutputLineCallCount = 0;
            int onErrorLineCallCount = 0;

            CommandResult result = new BuiltInCommand("fakeCommand", Enumerable.Empty<string>(), testCommand)
                .OnOutputLine(line =>
                {
                    onOutputLineCallCount++;

                    if (onOutputLineCallCount == 1)
                    {
                        Assert.Equal($"firstsecond", line);
                    }
                    else
                    {
                        Assert.Equal($"third", line);
                    }
                })
                .OnErrorLine(line =>
                {
                    onErrorLineCallCount++;

                    if (onErrorLineCallCount == 1)
                    {
                        Assert.Equal($"fourth", line);
                    }
                    else
                    {
                        Assert.Equal($"fifth", line);
                    }
                })
                .Execute();

            Assert.Equal(exitCode, result.ExitCode);
            Assert.Equal(2, onOutputLineCallCount);
            Assert.Equal(2, onErrorLineCallCount);
        }
    }
}
