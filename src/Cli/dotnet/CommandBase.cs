// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public abstract class CommandBase
    {
        protected CommandBase()
        {
        }

        protected CommandBase(ParseResult parseResult)
        {
            ShowHelpOrErrorIfAppropriate(parseResult);
        }

        protected virtual void ShowHelpOrErrorIfAppropriate(ParseResult parseResult)
        {
            parseResult.ShowHelpOrErrorIfAppropriate();
        }

        public abstract int Execute();
    }
}
