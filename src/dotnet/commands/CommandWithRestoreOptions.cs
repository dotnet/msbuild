// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.Restore;

namespace Microsoft.DotNet.Tools
{
    public static class CreateWithRestoreOptions
    {
        public static Command Command(
            string name,
            string help,
            ArgumentsRule arguments,
            params Option[] options)
        {
            return Create.Command(name, help, arguments, RestoreCommandParser.AddImplicitRestoreOptions(options));
        }

        public static Command Command(
            string name,
            string help,
            ArgumentsRule arguments,
            bool treatUnmatchedTokensAsErrors,
            params Option[] options)
        {
            return Create.Command(
                name,
                help,
                arguments,
                treatUnmatchedTokensAsErrors,
                RestoreCommandParser.AddImplicitRestoreOptions(options));
        }
    }
}