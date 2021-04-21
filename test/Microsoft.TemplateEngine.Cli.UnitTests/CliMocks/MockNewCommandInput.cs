// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Template;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal class MockNewCommandInput : INewCommandInput, IXunitSerializable
    {
        // a list of all the parameters defined by the template
        private IReadOnlyList<string> _allParametersForTemplate;

        private Dictionary<string, string> _templateOptions;
        private Dictionary<string, string> _commandOptions;

        public MockNewCommandInput(string templateName, string language = null, string type = null) : this()
        {
            TemplateName = templateName;
            if (!string.IsNullOrWhiteSpace(language))
            {
                _commandOptions["--language"] = language;
            }
            if (!string.IsNullOrWhiteSpace(type))
            {
                _commandOptions["--type"] = type;
            }
        }

        public MockNewCommandInput()
        {
            InputTemplateParams = new Dictionary<string, string>();
            RemainingParameters = new Dictionary<string, IList<string>>();
            RemainingArguments = new List<string>();
            _allParametersForTemplate = new List<string>();
            _commandOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _templateOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Alias { get; }

        public string AllowScriptsToRun { get; }

        public string AuthorFilter => _commandOptions.ContainsKey("--author") ? _commandOptions["--author"] : string.Empty;

        public string BaselineName => _commandOptions.ContainsKey("--baseline") ? _commandOptions["--baseline"] : string.Empty;

        public bool CheckForUpdates { get; }

        public bool ApplyUpdates { get; }

        public IReadOnlyCollection<string> Columns
        {
            get
            {
                if (_commandOptions.ContainsKey("--columns"))
                {
                    return _commandOptions["--columns"].Split(",");
                }
                return new List<string>();
            }
        }

        public string ColumnsParseError => throw new NotImplementedException();

        public string CommandName => "MockNew";

        public bool ExpandedExtraArgsFiles { get; }

        public IList<string> ExtraArgsFileNames { get; }

        public bool HasColumnsParseError => throw new NotImplementedException();

        public bool HasParseError { get; }

        public string HelpText { get; }

        // When using this mock, set the inputs using constructor input.
        // This property gets assigned based on the constructor input and the template being worked with.
        public IReadOnlyDictionary<string, string> InputTemplateParams { get; private set; }

        public IList<string> InstallNuGetSourceList { get; }

        public bool IsDryRun { get; }

        public bool IsForceFlagSpecified { get; }

        public bool IsHelpFlagSpecified => _commandOptions.ContainsKey("--help") || _commandOptions.ContainsKey("-h");

        public bool IsInteractiveFlagSpecified { get; }

        public bool IsListFlagSpecified => _commandOptions.ContainsKey("--list") || _commandOptions.ContainsKey("-l");

        public bool IsQuietFlagSpecified { get; }

        public bool IsShowAllFlagSpecified { get; }

        public string Language => _commandOptions.ContainsKey("--language") ? _commandOptions["--language"] : string.Empty;

        public string Name { get; }

        public string OutputPath { get; }

        public string PackageFilter => _commandOptions.ContainsKey("--package") ? _commandOptions["--package"] : string.Empty;

        // When using this mock, set the inputs using constructor input.
        // This property gets assigned based on the constructor input and the template being worked with.
        public List<string> RemainingArguments { get; private set; }

        // When using this mock, set the inputs using constructor input.
        // This property gets assigned based on the constructor input and the template being worked with.
        public IDictionary<string, IList<string>> RemainingParameters { get; private set; }

        public bool SearchOnline { get; }

        public string ShowAliasesAliasName { get; }

        public bool ShowAliasesSpecified { get; }

        public bool ShowAllColumns => _commandOptions.ContainsKey("--columns-all");

        public bool SkipUpdateCheck { get; }

        public string TagFilter => _commandOptions.ContainsKey("--tag") ? _commandOptions["--tag"] : string.Empty;

        public string TemplateName { get; private set; }

        public IList<string> ToInstallList { get; }

        public IReadOnlyList<string> Tokens { get; }

        public IList<string> ToUninstallList { get; }

        public string TypeFilter => _commandOptions.ContainsKey("--type") ? _commandOptions["--type"] : string.Empty;

        public MockNewCommandInput WithTemplateOption(string optionName, string optionValue = null)
        {
            _templateOptions[optionName] = optionValue;
            return this;
        }

        public MockNewCommandInput WithHelpOption()
        {
            _commandOptions["--help"] = null;
            return this;
        }

        public MockNewCommandInput WithListOption()
        {
            _commandOptions["--list"] = null;
            return this;
        }

        public MockNewCommandInput WithCommandOption(string optionName, string optionValue = null)
        {
            _commandOptions[optionName] = optionValue;
            return this;
        }

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

            foreach (KeyValuePair<string, string> inputParam in _templateOptions)
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
            return canonical;
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

        #region IXunitSerializable implementation

        public void Deserialize(IXunitSerializationInfo info)
        {
            TemplateName = info.GetValue<string>("command_templateName");
            _commandOptions = JsonConvert.DeserializeObject<Dictionary<string, string>>(info.GetValue<string>("command_options"));
            _templateOptions = JsonConvert.DeserializeObject<Dictionary<string, string>>(info.GetValue<string>("command_templateOptions"));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("command_templateName", TemplateName, typeof(string));
            info.AddValue("command_options", JsonConvert.SerializeObject(_commandOptions), typeof(string));
            info.AddValue("command_templateOptions", JsonConvert.SerializeObject(_templateOptions), typeof(string));
        }

        public override string ToString()
        {
            string result = TemplateName;
            result += " " + string.Join(" ", _commandOptions.Select(kvp => kvp.Key + (string.IsNullOrWhiteSpace(kvp.Value) ? string.Empty : " " + kvp.Value)));
            result += " " + string.Join(" ", _templateOptions.Select(kvp => kvp.Key + (string.IsNullOrWhiteSpace(kvp.Value) ? string.Empty : " " + kvp.Value)));
            return result;
        }

        #endregion IXunitSerializable implementation
    }
}
