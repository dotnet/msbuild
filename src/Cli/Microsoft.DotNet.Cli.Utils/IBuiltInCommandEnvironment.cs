// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    internal interface IBuiltInCommandEnvironment
    {
        TextWriter GetConsoleOut();
        void SetConsoleOut(TextWriter newOut);

        TextWriter GetConsoleError();
        void SetConsoleError(TextWriter newError);

        string GetWorkingDirectory();
        void SetWorkingDirectory(string path);
    }
}
