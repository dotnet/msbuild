using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    public interface INewCommandInput
    {
        string TemplateName { get; }

        string Alias { get; }

        IList<string> ExtraArgsFileNames { get; }

        IList<string> ToInstallList { get; }

        bool IsForceFlagSpecified { get; }

        bool IsHelpFlagSpecified { get; }

        bool IsListFlagSpecified { get; }

        bool IsQuietFlagSpecified { get; }

        bool IsShowAllFlagSpecified { get; }

        string TypeFilter { get; }

        string Language { get; }

        string Locale { get; }

        string Name { get; }

        string OutputPath { get; }

        bool SkipUpdateCheck { get; }

        string AllowScriptsToRun { get; }

        bool HasDebuggingFlag(string flag);

        IReadOnlyDictionary<string, string> AllTemplateParams { get; }

        string TemplateParamInputFormat(string canonical);

        IReadOnlyList<string> VariantsForCanonical(string canonical);

        bool TryGetCanonicalNameForVariant(string variant, out string canonical);

        List<string> RemainingArguments { get; }

        IDictionary<string, IList<string>> RemainingParameters { get; }

        bool TemplateParamHasValue(string paramName);

        string TemplateParamValue(string paramName);

        void ReparseForTemplate(ITemplateInfo templateInfo, HostSpecificTemplateData hostSpecificTemplateData);

        string HelpText { get; }

        int Execute(params string[] args);

        void OnExecute(Func<Task<CreationResultStatus>> invoke);

        bool HasParseError { get; }
    }
}
