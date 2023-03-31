// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.NET.Sdk.Razor.Tool.CommandLineUtils
{
    internal class CommandParsingException : Exception
    {
        public CommandParsingException(CommandLineApplication command, string message)
            : base(message)
        {
            Command = command;
        }

        public CommandLineApplication Command { get; }
    }
}
