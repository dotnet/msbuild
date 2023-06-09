// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public static Option<FileInfo> ProjectPathOption { get; } = new Option<FileInfo>("--project", SymbolStrings.Option_ProjectPath).AcceptExistingOnly();

        public static Option<bool> InteractiveOption { get; } = SharedOptionsFactory.CreateInteractiveOption();

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
