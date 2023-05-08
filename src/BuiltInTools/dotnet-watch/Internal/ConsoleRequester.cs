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
    public class ConsoleRequester : IRequester
    {
        private readonly object _writeLock = new object();

        public ConsoleRequester(IConsole console, bool quiet, bool suppressEmojis)
        {
            Ensure.NotNull(console, nameof(console));

            Console = console;
            IsQuiet = quiet;
            SuppressEmojis = suppressEmojis;
        }

        private IConsole Console { get; }
        private bool IsQuiet { get; set; }
        private bool SuppressEmojis { get; set; }

        public async Task<ConsoleKey> GetKeyAsync(string prompt, Func<ConsoleKey, bool> validateInput, CancellationToken cancellationToken)
        {
            if (IsQuiet)
            {
                return ConsoleKey.Escape;
            }

            var questionMark = SuppressEmojis ? "?" : "❔";
            while (true)
            {
                WriteLine($"  {questionMark} {prompt}");

                lock (_writeLock)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Out.Write($"  {questionMark} ");
                    Console.ResetColor();
                }
                
                var tcs = new TaskCompletionSource<ConsoleKey>(TaskCreationOptions.RunContinuationsAsynchronously);
                Console.KeyPressed += KeyPressed;
                try
                {
                    return await tcs.Task.WaitAsync(cancellationToken);
                }
                catch (ArgumentException)
                {
                    // Prompt again for valid input
                }
                finally
                {
                    Console.KeyPressed -= KeyPressed;
                }

                void KeyPressed(ConsoleKeyInfo key)
                {
                    if (validateInput(key.Key))
                    {
                        WriteLine(key.KeyChar.ToString());
                        tcs.TrySetResult(key.Key);
                    }
                    else
                    {
                        WriteLine(key.KeyChar.ToString(), ConsoleColor.DarkRed);
                        tcs.TrySetException(new ArgumentException($"Invalid key {key.KeyChar} entered."));
                    }
                }
            }
            
            void WriteLine(string message, ConsoleColor color = ConsoleColor.DarkGray)
            {
                lock (_writeLock)
                {
                    Console.ForegroundColor = color;
                    Console.Out.WriteLine(message);
                    Console.ResetColor();
                }
            }
        }
    }
}
