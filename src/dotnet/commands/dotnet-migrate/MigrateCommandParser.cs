// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class MigrateCommandParser
    {
        public static Command Migrate() =>
            Create.Command("migrate",
                           ".NET Migrate Command",
                           CommonOptions.HelpOption(),
                           Create.Option("-t|--template-file",
                                         "Base MSBuild template to use for migrated app. The default is the project included in dotnet new."),
                           Create.Option("-v|--sdk-package-version",
                                         "The version of the SDK package that will be referenced in the migrated app. The default is the version of the SDK in dotnet new."),
                           Create.Option("-x|--xproj-file",
                                         "The path to the xproj file to use. Required when there is more than one xproj in a project directory."),
                           Create.Option("-s|--skip-project-references",
                                         "Skip migrating project references. By default, project references are migrated recursively."),
                           Create.Option("-r|--report-file",
                                         "Output migration report to the given file in addition to the console."),
                           Create.Option("--format-report-file-json",
                                         "Output migration report file as json rather than user messages."),
                           Create.Option("--skip-backup",
                                         "Skip moving project.json, global.json, and *.xproj to a `backup` directory after successful migration."));
    }
}