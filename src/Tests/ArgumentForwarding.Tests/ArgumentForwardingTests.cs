// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Tests.ArgumentForwarding
{
    public class ArgumentForwardingTests : SdkTest
    {
        private static readonly string s_reflectorDllName = "ArgumentsReflector.dll";
        private static readonly string s_reflectorCmdName = "reflector_cmd";

        private string ReflectorPath { get; set; }
        private string ReflectorCmdPath { get; set; }

        public ArgumentForwardingTests(ITestOutputHelper log) : base(log)
        {
            // This test has a dependency on an argument reflector
            // Make sure it's been binplaced properly
            FindAndEnsureReflectorPresent();
        }

        private void FindAndEnsureReflectorPresent()
        {
            ReflectorPath = Path.Combine(AppContext.BaseDirectory, s_reflectorDllName);
            ReflectorCmdPath = Path.Combine(AppContext.BaseDirectory, s_reflectorCmdName);
            File.Exists(ReflectorPath).Should().BeTrue();
        }

        /// <summary>
        /// Tests argument forwarding in Command.Create
        /// This is a critical scenario for the driver.
        /// </summary>
        /// <param name="testUserArgument"></param>
        [Theory]
        [InlineData(@"""abc"" d e")]
        [InlineData(@"""ábc"" d é")]
        [InlineData(@"""abc""      d e")]
        [InlineData("\"abc\"\t\td\te")]
        [InlineData(@"a\\b d""e f""g h")]
        [InlineData(@"\ \\ \\\")]
        [InlineData(@"a\""b c d")]
        [InlineData(@"a\\""b c d")]
        [InlineData(@"a\\\""b c d")]
        [InlineData(@"a\\\\""b c d")]
        [InlineData(@"a\\\\""b c"" d e")]
        [InlineData(@"a""b c""d e""f g""h i""j k""l")]
        [InlineData(@"a b c""def")]
        [InlineData(@"""\a\"" \\""\\\ b c")]
        [InlineData(@"a\""b \\ cd ""\e f\"" \\""\\\")]
        public void TestArgumentForwarding(string testUserArgument)
        {
            // Get Baseline Argument Evaluation via Reflector
            var rawEvaluatedArgument = RawEvaluateArgumentString(testUserArgument);

            // Escape and Re-Evaluate the rawEvaluatedArgument
            var escapedEvaluatedRawArgument = EscapeAndEvaluateArgumentString(rawEvaluatedArgument);

            rawEvaluatedArgument.Length.Should().Be(escapedEvaluatedRawArgument.Length);

            for (int i=0; i<rawEvaluatedArgument.Length; ++i)
            {
                var rawArg = rawEvaluatedArgument[i];
                var escapedArg = escapedEvaluatedRawArgument[i];

                rawArg.Should().Be(escapedArg);
            }
        }

        /// <summary>
        /// Tests argument forwarding in Command.Create to a cmd file
        /// This is a critical scenario for the driver.
        /// </summary>
        /// <param name="testUserArgument"></param>
        [WindowsOnlyTheory]
        [InlineData(@"""abc"" d e")]
        [InlineData(@"""abc""      d e")]
        [InlineData("\"abc\"\t\td\te")]
        [InlineData(@"a\\b d""e f""g h")]
        [InlineData(@"\ \\ \\\")]
        [InlineData(@"a\\""b c d")]
        [InlineData(@"a\\\\""b c d")]
        [InlineData(@"a\\\\""b c"" d e")]
        [InlineData(@"a""b c""d e""f g""h i""j k""l")]
        [InlineData(@"a b c""def")]
        public void TestArgumentForwardingCmd(string testUserArgument)
        {
            // Get Baseline Argument Evaluation via Reflector
            // This does not need to be different for cmd because
            // it only establishes what the string[] args should be
            var rawEvaluatedArgument = RawEvaluateArgumentString(testUserArgument);

            // Escape and Re-Evaluate the rawEvaluatedArgument
            var escapedEvaluatedRawArgument = EscapeAndEvaluateArgumentStringCmd(rawEvaluatedArgument);

            try
            {
                rawEvaluatedArgument.Length.Should().Be(escapedEvaluatedRawArgument.Length);
            }
            catch(Exception)
            {
                Console.WriteLine("Argument Lists differ in length.");

                var expected = string.Join(",", rawEvaluatedArgument);
                var actual = string.Join(",", escapedEvaluatedRawArgument);
                Console.WriteLine($"Expected: {expected}");
                Console.WriteLine($"Actual: {actual}");

                throw;
            }

            for (int i = 0; i < rawEvaluatedArgument.Length; ++i)
            {
                var rawArg = rawEvaluatedArgument[i];
                var escapedArg = escapedEvaluatedRawArgument[i];

                try
                {
                    rawArg.Should().Be(escapedArg);
                }
                catch(Exception)
                {
                    Console.WriteLine($"Expected: {rawArg}");
                    Console.WriteLine($"Actual: {escapedArg}");
                    throw;
                }
            }
        }

        [WindowsOnlyTheory]
        [InlineData(@"a\""b c d")]
        [InlineData(@"a\\\""b c d")]
        [InlineData(@"""\a\"" \\""\\\ b c")]
        [InlineData(@"a\""b \\ cd ""\e f\"" \\""\\\")]
        public void TestArgumentForwardingCmdFailsWithUnbalancedQuote(string testArgString)
        {
            // Get Baseline Argument Evaluation via Reflector
            // This does not need to be different for cmd because
            // it only establishes what the string[] args should be
            var rawEvaluatedArgument = RawEvaluateArgumentString(testArgString);

            // Escape and Re-Evaluate the rawEvaluatedArgument
            var escapedEvaluatedRawArgument = EscapeAndEvaluateArgumentStringCmd(rawEvaluatedArgument);

            rawEvaluatedArgument.Length.Should().NotBe(escapedEvaluatedRawArgument.Length);
        }

        /// <summary>
        /// EscapeAndEvaluateArgumentString returns a representation of string[] args
        /// when rawEvaluatedArgument is passed as an argument to a process using
        /// CommandUsingResolver.Create(). Ideally this should escape the argument such that
        /// the output is == rawEvaluatedArgument.
        /// </summary>
        /// <param name="rawEvaluatedArgument">A string[] representing string[] args as already evaluated by a process</param>
        /// <returns></returns>
        private string[] EscapeAndEvaluateArgumentString(string[] rawEvaluatedArgument)
        {
            var commandResult = CommandFactoryUsingResolver.Create("dotnet", new[] { ReflectorPath }.Concat(rawEvaluatedArgument))
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            Console.WriteLine($"STDOUT: {commandResult.StdOut}");

            Console.WriteLine($"STDERR: {commandResult.StdErr}");

            commandResult.ExitCode.Should().Be(0);

            return ParseReflectorOutput(commandResult.StdOut);
        }

        /// <summary>
        /// EscapeAndEvaluateArgumentString returns a representation of string[] args
        /// when rawEvaluatedArgument is passed as an argument to a process using
        /// Command.Create(). Ideally this should escape the argument such that
        /// the output is == rawEvaluatedArgument.
        /// </summary>
        /// <param name="rawEvaluatedArgument">A string[] representing string[] args as already evaluated by a process</param>
        /// <returns></returns>
        private string[] EscapeAndEvaluateArgumentStringCmd(string[] rawEvaluatedArgument)
        {
            var cmd = CommandFactoryUsingResolver.Create(s_reflectorCmdName, rawEvaluatedArgument);
            var commandResult = cmd
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            Console.WriteLine(commandResult.StdOut);
            Console.WriteLine(commandResult.StdErr);

            commandResult.ExitCode.Should().Be(0);

            return ParseReflectorCmdOutput(commandResult.StdOut);
        }

        /// <summary>
        /// Parse the output of the reflector into a string array.
        /// Reflector output is simply string[] args written to
        /// one string separated by commas.
        /// </summary>
        /// <param name="reflectorOutput"></param>
        /// <returns></returns>
        private string[] ParseReflectorOutput(string reflectorOutput)
        {
            return reflectorOutput.TrimEnd('\r', '\n').Split(',');
        }

        /// <summary>
        /// Parse the output of the reflector into a string array.
        /// Reflector output is simply string[] args written to
        /// one string separated by commas.
        /// </summary>
        /// <param name="reflectorOutput"></param>
        /// <returns></returns>
        private string[] ParseReflectorCmdOutput(string reflectorOutput)
        {
            var args = reflectorOutput.Split(new string[] { "," }, StringSplitOptions.None);
            args[args.Length-1] = args[args.Length-1].TrimEnd('\r', '\n');

            // To properly pass args to cmd, quotes inside a parameter are doubled
            // Count them as a single quote for our comparison.
            for (int i=0; i < args.Length; ++i)
            {
                args[i] = args[i].Replace(@"""""", @"""");
            }
            return args;
        }

        /// <summary>
        /// RawEvaluateArgumentString returns a representation of string[] args
        /// when testUserArgument is provided (unmodified) as arguments to a c#
        /// process.
        /// </summary>
        /// <param name="testUserArgument">A test argument representing what a "user" would provide to a process</param>
        /// <returns>A string[] representing string[] args with the provided testUserArgument</returns>
        private string[] RawEvaluateArgumentString(string testUserArgument)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                    Arguments = $"{ReflectorPath} {testUserArgument}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            proc.WaitForExit();
            var stdOut = proc.StandardOutput.ReadToEnd();

            Assert.Equal(0, proc.ExitCode);

            return ParseReflectorOutput(stdOut);
        }
    }
}
