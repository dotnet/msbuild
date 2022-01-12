// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class ConsoleReporter : IReporter
    {
        private readonly object _writeLock = new object();

        public ConsoleReporter(IConsole console)
            : this(console, verbose: false, quiet: false)
        { }

        public ConsoleReporter(IConsole console, bool verbose, bool quiet)
        {
            Ensure.NotNull(console, nameof(console));

            Console = console;
            IsVerbose = verbose;
            IsQuiet = quiet;
        }

        protected IConsole Console { get; }
        public bool IsVerbose { get; set; }
        public bool IsQuiet { get; set; }

        private void WriteLine(TextWriter writer, string message, ConsoleColor? color, string emoji)
        {
            lock (_writeLock)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                writer.Write($"dotnet watch {emoji} ");
                Console.ResetColor();

                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                }

                writer.WriteLine(message);

                if (color.HasValue)
                {
                    Console.ResetColor();
                }
            }
        }

        public virtual void Error(string message, string emoji = "❌")
        {
            WriteLine(Console.Error, message, ConsoleColor.Red, emoji);
        }

        public virtual void Warn(string message, string emoji = "⌚")
        {
            WriteLine(Console.Out, message, ConsoleColor.Yellow, emoji);
        }

        public virtual void Output(string message, string emoji = "⌚")
        {
            if (IsQuiet)
            {
                return;
            }

            WriteLine(Console.Out, message, color: null, emoji);
        }

        public virtual void Verbose(string message, string emoji = "⌚")
        {
            if (!IsVerbose)
            {
                return;
            }

            WriteLine(Console.Out, message, ConsoleColor.DarkGray, emoji);
        }
    }
}
