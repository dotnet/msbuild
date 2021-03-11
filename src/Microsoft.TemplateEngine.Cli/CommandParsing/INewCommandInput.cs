using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    public interface INewCommandInput
    {
        string Alias { get; }
        string AllowScriptsToRun { get; }
        string AuthorFilter { get; }
        string BaselineName { get; }
        bool CheckForUpdates { get; }
        bool CheckForUpdatesNoPrompt { get; }
        IReadOnlyCollection<string> Columns { get; }
        string ColumnsParseError { get; }
        string CommandName { get; }
        bool ExpandedExtraArgsFiles { get; }
        IList<string> ExtraArgsFileNames { get; }
        bool HasColumnsParseError { get; }
        bool HasParseError { get; }
        string HelpText { get; }
        IReadOnlyDictionary<string, string> InputTemplateParams { get; }
        IList<string> InstallNuGetSourceList { get; }
        bool IsDryRun { get; }
        bool IsForceFlagSpecified { get; }
        bool IsHelpFlagSpecified { get; }
        bool IsInteractiveFlagSpecified { get; }
        bool IsListFlagSpecified { get; }
        bool IsQuietFlagSpecified { get; }
        bool IsShowAllFlagSpecified { get; }
        string Language { get; }
        string Name { get; }
        string OutputPath { get; }
        string PackageFilter { get; }
        List<string> RemainingArguments { get; }
        IDictionary<string, IList<string>> RemainingParameters { get; }
        bool SearchOnline { get; }
        string ShowAliasesAliasName { get; }
        bool ShowAliasesSpecified { get; }
        bool ShowAllColumns { get; }
        bool SkipUpdateCheck { get; }
        string TemplateName { get; }
        IList<string> ToInstallList { get; }
        IReadOnlyList<string> Tokens { get; }
        IList<string> ToUninstallList { get; }
        string TypeFilter { get; }

        int Execute(params string[] args);

        bool HasDebuggingFlag(string flag);

        void OnExecute(Func<Task<CreationResultStatus>> invoke);

        void ReparseForTemplate(ITemplateInfo templateInfo, HostSpecificTemplateData hostSpecificTemplateData);

        void ResetArgs(params string[] args);

        bool TemplateParamHasValue(string paramName);

        string TemplateParamInputFormat(string canonical);

        string TemplateParamValue(string paramName);

        bool TryGetCanonicalNameForVariant(string variant, out string canonical);

        IReadOnlyList<string> VariantsForCanonical(string canonical);
    }
}
