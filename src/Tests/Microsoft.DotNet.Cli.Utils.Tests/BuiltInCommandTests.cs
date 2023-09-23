// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public class BuiltInCommandTests
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

            var builtInCommand = new BuiltInCommand("fakeCommand", testCommandArgs, testCommand, new TestBuiltInCommandEnvironment());
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
            const int exitCode = 29;

            TestBuiltInCommandEnvironment environment = new();

            Func<string[], int> testCommand = args =>
            {
                TextWriter outWriter = environment.GetConsoleOut();
                outWriter.Write("first");
                outWriter.WriteLine("second");
                outWriter.WriteLine("third");

                TextWriter errorWriter = environment.GetConsoleError();
                errorWriter.WriteLine("fourth");
                errorWriter.WriteLine("fifth");

                return exitCode;
            };

            int onOutputLineCallCount = 0;
            int onErrorLineCallCount = 0;

            CommandResult result = new BuiltInCommand("fakeCommand", Enumerable.Empty<string>(), testCommand, environment)
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

        private class TestBuiltInCommandEnvironment : IBuiltInCommandEnvironment
        {
            private TextWriter _consoleOut;
            private TextWriter _consoleError;

            public TextWriter GetConsoleOut()
            {
                return _consoleOut;
            }

            public void SetConsoleOut(TextWriter newOut)
            {
                _consoleOut = newOut;
            }

            public TextWriter GetConsoleError()
            {
                return _consoleError;
            }

            public void SetConsoleError(TextWriter newError)
            {
                _consoleError = newError;
            }

            public string GetWorkingDirectory()
            {
                return Directory.GetCurrentDirectory();
            }

            public void SetWorkingDirectory(string path)
            {
                // no-op
            }
        }
    }
}
