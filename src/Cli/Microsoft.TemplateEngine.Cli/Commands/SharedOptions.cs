// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    public static class SharedOptions
    {
        public static CliOption<FileInfo> OutputOption { get; } = new("--output", "-o")
        {
            Description = SymbolStrings.Option_Output,
            Required = false,
            Arity = new ArgumentArity(1, 1)
        };

        public static CliOption<FileInfo> ProjectPathOption { get; } = new CliOption<FileInfo>("--project")
        {
            Description = SymbolStrings.Option_ProjectPath
        }.AcceptExistingOnly();

        public static CliOption<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

        internal static CliOption<bool> ForceOption { get; } = SharedOptionsFactory.CreateForceOption();

        internal static CliOption<string> NameOption { get; } = new("--name", "-n")
        {
            Description = SymbolStrings.TemplateCommand_Option_Name,
            Arity = new ArgumentArity(1, 1)
        };

        internal static CliOption<bool> DryRunOption { get; } = new("--dry-run")
        {
            Description = SymbolStrings.TemplateCommand_Option_DryRun,
            Arity = new ArgumentArity(0, 1)
        };

        internal static CliOption<bool> NoUpdateCheckOption { get; } = new("--no-update-check")
        {
            Description = SymbolStrings.TemplateCommand_Option_NoUpdateCheck,
            Arity = new ArgumentArity(0, 1)
        };
    }
}
