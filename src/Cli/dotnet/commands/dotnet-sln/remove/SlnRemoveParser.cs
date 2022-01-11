// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnRemoveParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new Argument<IEnumerable<string>>(LocalizableStrings.RemoveProjectPathArgumentName)
        {
            Description = LocalizableStrings.RemoveProjectPathArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static Command GetCommand()
        {
            var command = new Command("remove", LocalizableStrings.RemoveAppFullName);

            command.AddArgument(ProjectPathArgument);

            return command;
        }
    }
}
