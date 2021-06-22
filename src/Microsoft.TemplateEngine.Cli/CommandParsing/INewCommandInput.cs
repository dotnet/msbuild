// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    internal interface INewCommandInput
    {
        string Alias { get; }

        string? AllowScriptsToRun { get; }

        bool ApplyUpdates { get; }

        string AuthorFilter { get; }

        string BaselineName { get; }

        bool CheckForUpdates { get; }

        IReadOnlyList<string> Columns { get; }

        string ColumnsParseError { get; }

        string CommandName { get; }

        IEnumerable<string> Errors { get; }

        bool ExpandedExtraArgsFiles { get; }

        IReadOnlyList<string>? ExtraArgsFileNames { get; }

        bool HasColumnsParseError { get; }

        bool HasParseError { get; }

        string HelpText { get; }

        //IReadOnlyDictionary<string, string> InputTemplateParams { get; }

        IReadOnlyList<string>? InstallNuGetSourceList { get; }

        bool IsDryRun { get; }

        bool IsForceFlagSpecified { get; }

        bool IsHelpFlagSpecified { get; }

        bool IsInteractiveFlagSpecified { get; }

        bool IsListFlagSpecified { get; }

        bool IsQuietFlagSpecified { get; }

        bool IsShowAllFlagSpecified { get; }

        string Language { get; }

        string Name { get; }

        /// <summary>
        /// True when the user specified --no-update-check option.
        /// </summary>
        bool NoUpdateCheck { get; }

        string OutputPath { get; }

        string PackageFilter { get; }

        IReadOnlyList<string> RemainingParameters { get; }

        bool SearchOnline { get; }

        string ShowAliasesAliasName { get; }

        bool ShowAliasesSpecified { get; }

        bool ShowAllColumns { get; }

        string TagFilter { get; }

        string TemplateName { get; }

        IReadOnlyList<string>? ToInstallList { get; }

        IReadOnlyList<string> Tokens { get; }

        IReadOnlyList<string>? ToUninstallList { get; }

        string TypeFilter { get; }

        bool HasDebuggingFlag(string flag);
    }
}
