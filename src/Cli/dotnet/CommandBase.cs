// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    public abstract class CommandBase
    {
        protected ParseResult _parseResult;

        protected CommandBase(ParseResult parseResult)
        {
            _parseResult = parseResult;
            ShowHelpOrErrorIfAppropriate(parseResult);
        }

        protected virtual void ShowHelpOrErrorIfAppropriate(ParseResult parseResult)
        {
            parseResult.ShowHelpOrErrorIfAppropriate();
        }

        public abstract int Execute();
    }
}
