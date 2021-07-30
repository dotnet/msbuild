// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine.Parsing;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Format
{
    public abstract class AbstractFormatCommand
    {
        protected abstract string ParseFrom { get; }
        protected abstract List<string> AddArgs(ParseResult parseResult);

        public DotnetFormatForwardingApp FromArgs(ParseResult parseResult)
        {
            parseResult.ShowHelpOrErrorIfAppropriate();
            var dotnetFormatArgs = AddArgs(parseResult);
            return new DotnetFormatForwardingApp(dotnetFormatArgs);
        }

        public DotnetFormatForwardingApp FromArgs(string[] args)
        {
            var parser = Parser.Instance;
            var parseResult = parser.ParseFrom(ParseFrom, args);
            return FromArgs(parseResult);
        }

        public int RunCommand(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);
            return FromArgs(args).Execute();
        }
    }
}
