// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.List.ProjectToProjectReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListProjectToProjectReferencesCommandParser
    {
        public static readonly Argument Argument = new Argument<string>("argument") { Arity = ArgumentArity.ZeroOrOne, IsHidden = true };

        public static Command GetCommand()
        {
            var command = new Command("reference", LocalizableStrings.AppFullName);

            command.AddArgument(Argument);

            return command;
        }
    }
}
