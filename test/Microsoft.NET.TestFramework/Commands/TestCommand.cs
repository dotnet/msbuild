// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{


    public abstract class TestCommand
    {
        public ITestOutputHelper Log { get; }

        protected TestCommand(ITestOutputHelper log)
        {
            Log = log;
        }

        protected abstract ICommand CreateCommand(string[] args);

        public CommandResult Execute(params string[] args)
        {
            var result = CreateCommand(args)
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute();

            Log.WriteLine($"> {result.StartInfo.FileName} {result.StartInfo.Arguments}");
            Log.WriteLine(result.StdOut);

            if (!string.IsNullOrEmpty(result.StdErr))
            {
                Log.WriteLine("");
                Log.WriteLine("StdErr:");
                Log.WriteLine(result.StdErr);
            }

            if (result.ExitCode != 0)
            {
                Log.WriteLine($"Exit Code: {result.ExitCode}");
            }

            return result;
        }
    }
}