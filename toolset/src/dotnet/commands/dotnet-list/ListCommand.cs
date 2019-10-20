// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.List.ProjectToProjectReferences;
using Microsoft.DotNet.Tools.List.PackageReferences;

namespace Microsoft.DotNet.Tools.List
{
    public class ListCommand : DotNetTopLevelCommandBase
    {
        protected override string CommandName => "list";
        protected override string FullCommandNameLocalized => LocalizableStrings.NetListCommand;
        protected override string ArgumentName => Constants.ProjectArgumentName;
        protected override string ArgumentDescriptionLocalized => CommonLocalizableStrings.ProjectArgumentDescription;

        internal override Dictionary<string, Func<AppliedOption, CommandBase>> SubCommands =>
            new Dictionary<string, Func<AppliedOption, CommandBase>>
            {
                {
                    "reference",
                    o => new ListProjectToProjectReferencesCommand(o, ParseResult)
                },
                {
                    "package",
                    o => new ListPackageReferencesCommand(o, ParseResult)
                }
            };

        public static int Run(string[] args)
        {
            return new ListCommand().RunCommand(args);
        }
    }
}
