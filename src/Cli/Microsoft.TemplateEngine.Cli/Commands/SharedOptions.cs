// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    public static class SharedOptions
    {
        public static Option<FileInfo> OutputOption { get; } = new Option<FileInfo>(new string[] { "-o", "--output" })
        {
            Description = SymbolStrings.Option_Output,
            IsRequired = false,
            Arity = new ArgumentArity(1, 1)
        };

        public static Option<FileInfo> ProjectPathOption { get; } = new Option<FileInfo>("--project", SymbolStrings.Option_ProjectPath).ExistingOnly();

        internal static Option<bool> ForceOption { get; } = SharedOptionsFactory.CreateForceOption();

        internal static Option<string> NameOption { get; } = new Option<string>(new string[] { "-n", "--name" })
        {
            Description = SymbolStrings.TemplateCommand_Option_Name,
            Arity = new ArgumentArity(1, 1)
        };

        internal static Option<bool> DryRunOption { get; } = new Option<bool>("--dry-run")
        {
            Description = SymbolStrings.TemplateCommand_Option_DryRun,
            Arity = new ArgumentArity(0, 1)
        };

        internal static Option<bool> NoUpdateCheckOption { get; } = new Option<bool>("--no-update-check")
        {
            Description = SymbolStrings.TemplateCommand_Option_NoUpdateCheck,
            Arity = new ArgumentArity(0, 1)
        };
    }
}
