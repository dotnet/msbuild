using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal class MockNewCommandInput : INewCommandInput
    {
        // a list of all the parameters defined by the template
        private IReadOnlyList<string> _allParametersForTemplate;

        private IReadOnlyDictionary<string, string> _rawParameterInputs;

        public MockNewCommandInput() : this(new Dictionary<string, string>())
        {
        }

        public MockNewCommandInput(IReadOnlyDictionary<string, string> rawParameterInputs)
        {
            _rawParameterInputs = rawParameterInputs;

            InputTemplateParams = new Dictionary<string, string>();
            RemainingParameters = new Dictionary<string, IList<string>>();
            RemainingArguments = new List<string>();
            _allParametersForTemplate = new List<string>();
        }
        public string Alias { get; set; }

        public string AllowScriptsToRun { get; set; }

        public string AuthorFilter { get; set; }

        public string BaselineName { get; set; }

        public bool CheckForUpdates { get; set; }

        public bool CheckForUpdatesNoPrompt { get; set; }

        public IReadOnlyCollection<string> Columns { get; set; } = new List<string>();

        public string ColumnsParseError => throw new NotImplementedException();

        public string CommandName => "MockNew";

        public bool ExpandedExtraArgsFiles { get; set; }

        public IList<string> ExtraArgsFileNames { get; set; }

        public bool HasColumnsParseError => throw new NotImplementedException();

        public bool HasParseError { get; set; }

        public string HelpText { get; set; }

        // When using this mock, set the inputs using constructor input.
        // This property gets assigned based on the constructor input and the template being worked with.
        public IReadOnlyDictionary<string, string> InputTemplateParams { get; private set; }

        public IList<string> InstallNuGetSourceList { get; set; }

        public bool IsDryRun { get; set; }

        public bool IsForceFlagSpecified { get; set; }

        public bool IsHelpFlagSpecified { get; set; }

        public bool IsInteractiveFlagSpecified { get; set; }

        public bool IsListFlagSpecified { get; set; }

        public bool IsQuietFlagSpecified { get; set; }

        public bool IsShowAllFlagSpecified { get; set; }

        public string Language { get; set; }

        public string Locale { get; set; }

        public string Name { get; set; }

        public string OutputPath { get; set; }

        public string PackageFilter { get; set; }

        // When using this mock, set the inputs using constructor input.
        // This property gets assigned based on the constructor input and the template being worked with.
        public List<string> RemainingArguments { get; private set; }

        // When using this mock, set the inputs using constructor input.
        // This property gets assigned based on the constructor input and the template being worked with.
        public IDictionary<string, IList<string>> RemainingParameters { get; private set; }

        public bool SearchOnline { get; set; }

        public string ShowAliasesAliasName { get; set; }

        public bool ShowAliasesSpecified { get; set; }

        public bool ShowAllColumns { get; set; } = false;

        public bool SkipUpdateCheck { get; set; }

        public string TemplateName { get; set; }

        public IList<string> ToInstallList { get; set; }

        public IReadOnlyList<string> Tokens { get; set; }

        public IList<string> ToUninstallList { get; set; }

        public string TypeFilter { get; set; }

        public int Execute(params string[] args)
        {
            throw new NotImplementedException();
        }

        public bool HasDebuggingFlag(string flag)
        {
            throw new NotImplementedException();
        }

        public void OnExecute(Func<Task<CreationResultStatus>> invoke)
        {
            throw new NotImplementedException();
        }

        public void ReparseForTemplate(ITemplateInfo templateInfo, HostSpecificTemplateData hostSpecificTemplateData)
        {
            Dictionary<string, string> templateParamValues = new Dictionary<string, string>();
            Dictionary<string, IList<string>> remainingParams = new Dictionary<string, IList<string>>();

            Dictionary<string, string> overrideToCanonicalMap = hostSpecificTemplateData.LongNameOverrides.ToDictionary(o => o.Value, o => o.Key);
            foreach (KeyValuePair<string, string> shortNameOverride in hostSpecificTemplateData.ShortNameOverrides)
            {
                overrideToCanonicalMap[shortNameOverride.Value] = shortNameOverride.Key;
            }

            foreach (KeyValuePair<string, string> inputParam in _rawParameterInputs)
            {
                ITemplateParameter matchedParam = default(ITemplateParameter);

                if (templateInfo.Parameters != null)
                {
                    matchedParam = templateInfo.Parameters?.FirstOrDefault(x => string.Equals(x.Name, inputParam.Key));
                }

                if (matchedParam != default(ITemplateParameter))
                {
                    templateParamValues.Add(inputParam.Key, inputParam.Value);
                }
                else if (overrideToCanonicalMap.TryGetValue(inputParam.Key, out string canonical))
                {
                    templateParamValues.Add(canonical, inputParam.Value);
                }
                else
                {
                    remainingParams.Add(inputParam.Key, new List<string>());
                }
            }

            InputTemplateParams = templateParamValues;
            RemainingParameters = remainingParams;
            RemainingArguments = remainingParams.Keys.ToList();

            _allParametersForTemplate = templateInfo.Parameters.Select(x => x.Name).ToList();
        }

        public void ResetArgs(params string[] args)
        {
            throw new NotImplementedException();
        }

        public bool TemplateParamHasValue(string paramName)
        {
            throw new NotImplementedException();
        }

        public string TemplateParamInputFormat(string canonical)
        {
            throw new NotImplementedException();
        }

        public string TemplateParamValue(string paramName)
        {
            throw new NotImplementedException();
        }

        // Note: This doesn't really deal with variants.
        // If the input "variant" is a parameter for the template, return true with the canonical set to the variant.
        // Otherwise return false with the canonical as null.
        public bool TryGetCanonicalNameForVariant(string variant, out string canonical)
        {
            if (_allParametersForTemplate.Contains(variant))
            {
                canonical = variant;
                return true;
            }

            canonical = null;
            return false;
        }

        public IReadOnlyList<string> VariantsForCanonical(string canonical)
        {
            throw new NotImplementedException();
        }
    }
}
