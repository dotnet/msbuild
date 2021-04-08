// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.TemplateEngine.Cli
{
    public class CommandParserException : Exception
    {
        internal CommandParserException(string message, string argument)
            : base(message)
        {
            Argument = argument;
        }

        internal CommandParserException(string message, string argument, Exception innerException)
            : base(message, innerException)
        {
            Argument = argument;
        }

        public string Argument { get; }
    }
}
