// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Cli.CommandParsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommandArgs : INewCommandInput
    {
        public string Alias => throw new NotImplementedException();

        public string? AllowScriptsToRun => throw new NotImplementedException();

        public bool ApplyUpdates => throw new NotImplementedException();

        public string AuthorFilter => throw new NotImplementedException();

        public string BaselineName => throw new NotImplementedException();

        public bool CheckForUpdates => throw new NotImplementedException();

        public IReadOnlyList<string> Columns => throw new NotImplementedException();

        public string ColumnsParseError => throw new NotImplementedException();

        public IEnumerable<string> Errors => throw new NotImplementedException();

        public bool ExpandedExtraArgsFiles => throw new NotImplementedException();

        public IReadOnlyList<string>? ExtraArgsFileNames => throw new NotImplementedException();

        public bool HasColumnsParseError => throw new NotImplementedException();

        public string HelpText => throw new NotImplementedException();

        public IReadOnlyList<string>? InstallNuGetSourceList => AdditionalNuGetSources;

        public bool IsDryRun => throw new NotImplementedException();

        public bool IsForceFlagSpecified => throw new NotImplementedException();

        public bool IsHelpFlagSpecified => throw new NotImplementedException();

        public bool IsInteractiveFlagSpecified => Interactive;

        public bool IsListFlagSpecified => throw new NotImplementedException();

        public bool IsQuietFlagSpecified => throw new NotImplementedException();

        public bool IsSearchFlagSpecified => throw new NotImplementedException();

        public bool IsShowAllFlagSpecified => throw new NotImplementedException();

        public string Language => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public bool NoUpdateCheck => throw new NotImplementedException();

        public string OutputPath => throw new NotImplementedException();

        public string PackageFilter => throw new NotImplementedException();

        public IReadOnlyList<string> RemainingParameters => throw new NotImplementedException();

        public string ShowAliasesAliasName => throw new NotImplementedException();

        public bool ShowAliasesSpecified => throw new NotImplementedException();

        public bool ShowAllColumns => throw new NotImplementedException();

        public string TagFilter => throw new NotImplementedException();

        public string TemplateName => throw new NotImplementedException();

        public IReadOnlyList<string>? ToInstallList => InstallItems;

        public IReadOnlyList<string> Tokens => throw new NotImplementedException();

        public IReadOnlyList<string>? ToUninstallList => throw new NotImplementedException();

        public string TypeFilter => throw new NotImplementedException();

        public string? SearchNameCriteria => throw new NotImplementedException();

        public string? ListNameCriteria => throw new NotImplementedException();

        string INewCommandInput.CommandName => CommandName;

        public bool HasDebuggingFlag(string flag) => throw new NotImplementedException();

        public bool ValidateParseError() => throw new NotImplementedException();
    }
}
