// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Cli
{
    internal class CommandCreationException : Exception
    {
        public int ExitCode { get; private set; }

        public CommandCreationException(int exitCode)
        {
            ExitCode = exitCode;
        }
    }
}
