// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Microsoft.DotNet.CommandUtils
{
    internal sealed class CommandResultAssertions
    {
        private readonly CommandResult _commandResult;

        internal CommandResultAssertions(CommandResult commandResult)
        {
            _commandResult = commandResult;
        }

        internal AndConstraint<CommandResultAssertions> ExitWith(int expectedExitCode)
        {
            Execute.Assertion.ForCondition(_commandResult.ExitCode == expectedExitCode)
                .FailWith(AppendDiagnosticsTo($"Expected command to exit with {expectedExitCode} but it did not."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> Pass()
        {
            Execute.Assertion.ForCondition(_commandResult.ExitCode == 0)
                .FailWith(AppendDiagnosticsTo($"Expected command to pass but it did not."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> Fail()
        {
            Execute.Assertion.ForCondition(_commandResult.ExitCode != 0)
                .FailWith(AppendDiagnosticsTo($"Expected command to fail but it did not."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdOut()
        {
            Execute.Assertion.ForCondition(!string.IsNullOrEmpty(_commandResult.StdOut))
                .FailWith(AppendDiagnosticsTo("Command did not output anything to stdout"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdOut(string expectedOutput)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(_commandResult.StdOut.Equals(expectedOutput, StringComparison.Ordinal))
                .FailWith(AppendDiagnosticsTo($"Command did not output with Expected Output. Expected: {expectedOutput}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdOutContaining(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(_commandResult.StdOut.Contains(pattern))
                .FailWith(AppendDiagnosticsTo($"The command output did not contain expected result: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdOutContaining(Func<string, bool> predicate, string description = "")
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(predicate(_commandResult.StdOut))
                .FailWith(AppendDiagnosticsTo($"The command output did not contain expected result: {description} {Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> NotHaveStdOutContaining(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(!_commandResult.StdOut.Contains(pattern))
                .FailWith(AppendDiagnosticsTo($"The command output contained a result it should not have contained: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdOutContainingIgnoreSpaces(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            string commandResultNoSpaces = _commandResult.StdOut.Replace(" ", string.Empty);

            Execute.Assertion
                .ForCondition(commandResultNoSpaces.Contains(pattern))
                .FailWith(AppendDiagnosticsTo($"The command output did not contain expected result: {pattern}{Environment.NewLine}"));

            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdOutContainingIgnoreCase(string pattern)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(_commandResult.StdOut.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .FailWith(AppendDiagnosticsTo($"The command output did not contain expected result (ignoring case): {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(Regex.Match(_commandResult.StdOut, pattern, options).Success)
                .FailWith(AppendDiagnosticsTo($"Matching the command output failed. Pattern: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> NotHaveStdOutMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(!Regex.Match(_commandResult.StdOut, pattern, options).Success)
                .FailWith(AppendDiagnosticsTo($"The command output matched a pattern it should not have. Pattern: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdErr()
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(!string.IsNullOrEmpty(_commandResult.StdErr))
                .FailWith(AppendDiagnosticsTo("Command did not output anything to StdErr."));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdErr(string expectedOutput)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Execute.Assertion.ForCondition(_commandResult.StdErr.Equals(expectedOutput, StringComparison.Ordinal))
                .FailWith(AppendDiagnosticsTo($"Command did not output the expected output to StdErr.{Environment.NewLine}Expected: {expectedOutput}{Environment.NewLine}Actual:   {_commandResult.StdErr}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdErrContaining(string pattern)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Execute.Assertion.ForCondition(_commandResult.StdErr.Contains(pattern))
                .FailWith(AppendDiagnosticsTo($"The command error output did not contain expected result: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> NotHaveStdErrContaining(string pattern)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Execute.Assertion.ForCondition(!_commandResult.StdErr.Contains(pattern))
                .FailWith(AppendDiagnosticsTo($"The command error output contained a result it should not have contained: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveStdErrMatching(string pattern, RegexOptions options = RegexOptions.None)
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Execute.Assertion.ForCondition(Regex.Match(_commandResult.StdErr, pattern, options).Success)
                .FailWith(AppendDiagnosticsTo($"Matching the command error output failed. Pattern: {pattern}{Environment.NewLine}"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> NotHaveStdOut()
        {
            if (_commandResult.StdOut is null)
            {
                throw new InvalidOperationException("StdOut for the command was not captured");
            }
            Execute.Assertion.ForCondition(string.IsNullOrEmpty(_commandResult.StdOut))
                .FailWith(AppendDiagnosticsTo($"Expected command to not output to stdout but it was not:"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> NotHaveStdErr()
        {
            if (_commandResult.StdErr is null)
            {
                throw new InvalidOperationException("StdErr for the command was not captured");
            }
            Execute.Assertion.ForCondition(string.IsNullOrEmpty(_commandResult.StdErr))
                .FailWith(AppendDiagnosticsTo("Expected command to not output to stderr but it was not:"));
            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveSkippedProjectCompilation(string skippedProject, string frameworkFullName)
        {
            _commandResult.StdOut.Should().Contain($"Project {skippedProject} ({frameworkFullName}) was previously compiled. Skipping compilation.");

            return new AndConstraint<CommandResultAssertions>(this);
        }

        internal AndConstraint<CommandResultAssertions> HaveCompiledProject(string compiledProject, string frameworkFullName)
        {
            _commandResult.StdOut.Should().Contain($"Project {compiledProject} ({frameworkFullName}) will be compiled");

            return new AndConstraint<CommandResultAssertions>(this);
        }

        private string AppendDiagnosticsTo(string s)
        {
            return (s + $"{Environment.NewLine}" +
                       $"File Name: {_commandResult.StartInfo.FileName}{Environment.NewLine}" +
                       $"Arguments: {_commandResult.StartInfo.Arguments}{Environment.NewLine}" +
                       $"Exit Code: {_commandResult.ExitCode}{Environment.NewLine}" +
                       $"StdOut:{Environment.NewLine}{_commandResult.StdOut}{Environment.NewLine}" +
                       $"StdErr:{Environment.NewLine}{_commandResult.StdErr}{Environment.NewLine}")
                       //escape curly braces for String.Format
                       .Replace("{", "{{").Replace("}", "}}");
        }
    }
}
