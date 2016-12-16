// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.CommandLine
{
    internal class CommandParsingException : Exception
    {
        public CommandParsingException(
            CommandLineApplication command,
            string message,
            bool isRequireSubCommandMissing = false)
            : base(message)
        {
            Command = command;
            IsRequireSubCommandMissing = isRequireSubCommandMissing;
        }

        public CommandLineApplication Command { get; }
        public bool IsRequireSubCommandMissing { get; }
    }
}
