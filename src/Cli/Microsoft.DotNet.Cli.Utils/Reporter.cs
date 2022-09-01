// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Microsoft.DotNet.Cli.Utils
{
    // Stupid-simple console manager
    public class Reporter : IReporter
    {
        private static readonly object s_lock = new();

        //cannot use auto properties, as those are static
#pragma warning disable IDE0032 // Use auto property
        private static readonly Reporter s_consoleOutReporter = new(AnsiConsole.GetOutput());
        private static readonly Reporter s_consoleErrReporter = new(AnsiConsole.GetError());
#pragma warning restore IDE0032 // Use auto property

        private static IReporter s_errorReporter = s_consoleErrReporter;
        private static IReporter s_outputReporter = s_consoleOutReporter;
        private static IReporter s_verboseReporter = s_consoleOutReporter;

        private readonly AnsiConsole? _console;

        static Reporter()
        {
            Reset();
        }

        private Reporter(AnsiConsole? console)
        {
            _console = console;
        }

        public static Reporter NullReporter { get; } = new(console: null);
        public static Reporter ConsoleOutReporter => s_consoleOutReporter;
        public static Reporter ConsoleErrReporter => s_consoleErrReporter;

        public static IReporter Output { get; private set; } = NullReporter;
        public static IReporter Error { get; private set; } = NullReporter;
        public static IReporter Verbose { get; private set; } = NullReporter;

        /// <summary>
        /// Resets the reporters to write to the current reporters based on <see cref="CommandLoggingContext"/> settings.
        /// </summary>
        public static void Reset()
        {
            lock (s_lock)
            {
                ResetOutput();
                ResetError();
                ResetVerbose();
            }
        }

        /// <summary>
        /// Sets the output reporter to <paramref name="reporter"/>.
        /// The reporter won't be applied if disabled in <see cref="CommandLoggingContext"/>.
        /// </summary>
        /// <param name="reporter"></param>
        public static void SetOutput(IReporter reporter)
        {
            lock (s_lock)
            {
                s_outputReporter = reporter;
                ResetOutput();
            }
        }

        /// <summary>
        /// Sets the error reporter to <paramref name="reporter"/>.
        /// The reporter won't be applied if disabled in <see cref="CommandLoggingContext"/>.
        /// </summary>
        public static void SetError(IReporter reporter)
        {
            lock (s_lock)
            {
                s_errorReporter = reporter;
                ResetError();
            }
        }

        /// <summary>
        /// Sets the verbose reporter to <paramref name="reporter"/>.
        /// The reporter won't be applied if disabled in <see cref="CommandLoggingContext"/>.
        /// </summary>
        public static void SetVerbose(IReporter reporter)
        {
            lock (s_lock)
            {
                s_verboseReporter = reporter;
                ResetVerbose();
            }
        }

        private static void ResetOutput()
        {
            Output = CommandLoggingContext.OutputEnabled ? s_outputReporter : NullReporter;
        }

        private static void ResetError()
        {
            Error = CommandLoggingContext.ErrorEnabled ? s_errorReporter : NullReporter;
        }

        private static void ResetVerbose()
        {
            Verbose = CommandLoggingContext.IsVerbose ? s_verboseReporter : NullReporter;
        }

        public void WriteLine(string message)
        {
            lock (s_lock)
            {
                if (CommandLoggingContext.ShouldPassAnsiCodesThrough)
                {
                    _console?.Writer?.WriteLine(message);
                }
                else
                {
                    _console?.WriteLine(message);
                }
            }
        }

        public void WriteLine()
        {
            lock (s_lock)
            {
                _console?.Writer?.WriteLine();
            }
        }

        public void Write(string message)
        {
            lock (s_lock)
            {
                if (CommandLoggingContext.ShouldPassAnsiCodesThrough)
                {
                    _console?.Writer?.Write(message);
                }
                else
                {
                    _console?.Write(message);
                }
            }
        }

        public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));

    }
}
