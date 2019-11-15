// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

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

        string CommandName { get; }

        string CommandArgs { get; }
    }
}
