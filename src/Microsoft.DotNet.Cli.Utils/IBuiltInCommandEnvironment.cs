// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

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
