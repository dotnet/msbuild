// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Migrate;
using LocalizableStrings = Microsoft.DotNet.Tools.Migrate.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class MigrateCommandParser
    {
        public static Command Migrate() =>
            Create.Command(
                "migrate",
                ".NET Migrate Command",
                Accept.ZeroOrOneArgument()
                      .With(name: LocalizableStrings.CmdProjectArgument,
                            description: LocalizableStrings.CmdProjectArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option("-t|--template-file",
                              LocalizableStrings.CmdTemplateDescription,
                              Accept.ExactlyOneArgument()),
                Create.Option("-v|--sdk-package-version",
                              LocalizableStrings.CmdVersionDescription,
                              Accept.ExactlyOneArgument()),
                Create.Option("-x|--xproj-file",
                              LocalizableStrings.CmdXprojFileDescription,
                              Accept.ExactlyOneArgument()),
                Create.Option("-s|--skip-project-references",
                              LocalizableStrings.CmdSkipProjectReferencesDescription),
                Create.Option("-r|--report-file",
                              LocalizableStrings.CmdReportFileDescription,
                              Accept.ExactlyOneArgument()),
                Create.Option("--format-report-file-json",
                              LocalizableStrings.CmdReportOutputDescription),
                Create.Option("--skip-backup",
                              LocalizableStrings.CmdSkipBackupDescription));
    }
}