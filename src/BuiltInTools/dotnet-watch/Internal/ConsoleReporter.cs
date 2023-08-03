// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable


namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal sealed class ConsoleReporter : IReporter
    {
        private readonly object _writeLock = new object();

        public ConsoleReporter(IConsole console)
            : this(console, verbose: false, quiet: false, suppressEmojis: false)
        { }

        public ConsoleReporter(IConsole console, bool verbose, bool quiet, bool suppressEmojis)
        {
            Ensure.NotNull(console, nameof(console));

            Console = console;
            IsVerbose = verbose;
            IsQuiet = quiet;
            SuppressEmojis = suppressEmojis;
        }

        private IConsole Console { get; }
        public bool IsVerbose { get; set; }
        public bool IsQuiet { get; set; }
        public bool SuppressEmojis { get; set; }

        private void WriteLine(TextWriter writer, string message, ConsoleColor? color, string emoji)
        {
            lock (_writeLock)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                writer.Write($"dotnet watch {(SuppressEmojis ? ":" : emoji)} ");
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

        public void Error(string message, string emoji = "❌")
        {
            WriteLine(Console.Error, message, ConsoleColor.Red, emoji);
        }

        public void Warn(string message, string emoji = "⌚")
        {
            WriteLine(Console.Out, message, ConsoleColor.Yellow, emoji);
        }

        public void Output(string message, string emoji = "⌚")
        {
            if (IsQuiet)
            {
                return;
            }

            WriteLine(Console.Out, message, color: null, emoji);
        }

        public void Verbose(string message, string emoji = "⌚")
        {
            if (!IsVerbose)
            {
                return;
            }

            WriteLine(Console.Out, message, ConsoleColor.DarkGray, emoji);
        }
    }
}
