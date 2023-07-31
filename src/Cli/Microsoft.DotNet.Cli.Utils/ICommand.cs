// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public interface ICommand
    {
        CommandResult Execute();

        ICommand WorkingDirectory(string projectDirectory);

        ICommand EnvironmentVariable(string name, string value);

        ICommand CaptureStdOut();

        ICommand CaptureStdErr();

        ICommand ForwardStdOut(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true);

        ICommand ForwardStdErr(TextWriter to = null, bool onlyIfVerbose = false, bool ansiPassThrough = true);

        ICommand OnOutputLine(Action<string> handler);

        ICommand OnErrorLine(Action<string> handler);

        ICommand SetCommandArgs(string commandArgs);

        string CommandName { get; }

        string CommandArgs { get; }
    }
}
