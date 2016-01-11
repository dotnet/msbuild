// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class CommandResultAssertions
    {
        private CommandResult _commandResult;

        public CommandResultAssertions(CommandResult commandResult)
        {
            _commandResult = commandResult;
        }

        public AndConstraint<CommandResultAssertions> ExitWith(int expectedExitCode)
        {
            Execute.Assertion.ForCondition(_commandResult.ExitCode == expectedExitCode)
                .FailWith("Expected command to exit with {0} but it exited with {1}.", expectedExitCode, _commandResult.ExitCode);
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> Pass()
        {
            Execute.Assertion.ForCondition(_commandResult.ExitCode == 0)
                .FailWith("Expected command to pass but it exited with {0}.", _commandResult.ExitCode);
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> Fail()
        {
            Execute.Assertion.ForCondition(_commandResult.ExitCode != 0)
                .FailWith("Expected command to fail but it exited with {0}.", _commandResult.ExitCode);
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOut()
        {
            Execute.Assertion.ForCondition(!string.IsNullOrEmpty(_commandResult.StdOut))
                .FailWith("Command did not output anything to stdout");
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdOut(string expectedOutput)
        {
            Execute.Assertion.ForCondition(_commandResult.StdOut.Equals(expectedOutput, StringComparison.Ordinal))
                .FailWith($"Command did not output with Expected Output. Expected: {expectedOutput} Actual: {_commandResult.StdOut}");
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> StdOutMatchPattern(string pattern, RegexOptions options = RegexOptions.None)
        {
            Execute.Assertion.ForCondition(Regex.Match(_commandResult.StdOut, pattern, options).Success)
                .FailWith($"Matching the command output failed. Pattern: {pattern}{Environment.NewLine} input: {_commandResult.StdOut}");
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> HaveStdErr()
        {
            Execute.Assertion.ForCondition(!string.IsNullOrEmpty(_commandResult.StdErr))
                .FailWith("Command did not output anything to stderr");
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NotHaveStdOut()
        {
            Execute.Assertion.ForCondition(string.IsNullOrEmpty(_commandResult.StdOut))
                .FailWith("Expected command to not output to stdout but found - {0}{1}", Environment.NewLine, _commandResult.StdOut);
            return new AndConstraint<CommandResultAssertions>(this);
        }

        public AndConstraint<CommandResultAssertions> NotHaveStdErr()
        {
            Execute.Assertion.ForCondition(string.IsNullOrEmpty(_commandResult.StdErr))
                .FailWith("Expected command to not output to stderr but found - {0}{1}", Environment.NewLine, _commandResult.StdErr);
            return new AndConstraint<CommandResultAssertions>(this);
        }
    }
}
