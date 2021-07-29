// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Cli.CommandParsing;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Cli.UnitTests.CliMocks
{
    internal class MockNewCommandInput : INewCommandInput, IXunitSerializable
    {
        private Dictionary<string, string?> _templateOptions;
        private Dictionary<string, string?> _commandOptions;

        public MockNewCommandInput(string templateName, string? language = null, string? type = null) : this()
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
            _commandOptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            _templateOptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        public string Alias { get; } = string.Empty;

        public string AllowScriptsToRun { get; } = string.Empty;

        public string AuthorFilter => GetCommandOption("--author");

        public string BaselineName => GetCommandOption("--baseline");

        public bool CheckForUpdates { get; }

        public bool ApplyUpdates { get; }

        public IReadOnlyList<string> Columns
        {
            get
            {
                if (_commandOptions.ContainsKey("--columns"))
                {
                    return _commandOptions["--columns"]?.Split(",") ?? Array.Empty<string>();
                }
                return Array.Empty<string>();
            }
        }

        public string ColumnsParseError => throw new NotImplementedException();

        public string CommandName => "MockNew";

        public bool ExpandedExtraArgsFiles { get; }

        public IReadOnlyList<string> ExtraArgsFileNames { get; } = Array.Empty<string>();

        public bool HasColumnsParseError => throw new NotImplementedException();

        public bool HasParseError { get; }

        public string HelpText => throw new NotImplementedException();

        public IReadOnlyList<string> InstallNuGetSourceList { get; } = Array.Empty<string>();

        public bool IsDryRun { get; }

        public bool IsForceFlagSpecified { get; }

        public bool IsHelpFlagSpecified => _commandOptions.ContainsKey("--help") || _commandOptions.ContainsKey("-h");

        public bool IsInteractiveFlagSpecified { get; }

        public bool IsListFlagSpecified => _commandOptions.ContainsKey("--list") || _commandOptions.ContainsKey("-l");

        public bool IsQuietFlagSpecified { get; }

        public bool IsShowAllFlagSpecified { get; }

        public string Language => GetCommandOption("--language");

        public string Name { get; } = string.Empty;

        public string OutputPath { get; } = string.Empty;

        public string PackageFilter => GetCommandOption("--package");

        // When using this mock, set the inputs using constructor input.
        // This property gets assigned based on the constructor input and the template being worked with.
        public IReadOnlyList<string> RemainingParameters
        {
            get
            {
                List<string> remainingParams = new List<string>();
                foreach (var option in _templateOptions)
                {
                    remainingParams.Add(option.Key);
                    if (!string.IsNullOrWhiteSpace(option.Value))
                    {
                        remainingParams.Add(option.Value);
                    }
                }
                return remainingParams;
            }
        }

        public bool IsSearchFlagSpecified { get; }

        public string ShowAliasesAliasName { get; } = string.Empty;

        public bool ShowAliasesSpecified { get; }

        public bool ShowAllColumns => _commandOptions.ContainsKey("--columns-all");

        public bool SkipUpdateCheck { get; }

        public string TagFilter => GetCommandOption("--tag");

        public string TemplateName { get; private set; } = string.Empty;

        public IReadOnlyList<string> ToInstallList { get; } = Array.Empty<string>();

        public IReadOnlyList<string> Tokens
        {
            get
            {
                List<string> tokens = new List<string>() { CommandName };
                if (!string.IsNullOrWhiteSpace(TemplateName))
                {
                    tokens.Add(TemplateName);
                }
                foreach (var option in _commandOptions)
                {
                    tokens.Add(option.Key);
                    if (!string.IsNullOrWhiteSpace(option.Value))
                    {
                        tokens.Add(option.Value);
                    }    
                }
                foreach (var option in _templateOptions)
                {
                    tokens.Add(option.Key);
                    if (!string.IsNullOrWhiteSpace(option.Value))
                    {
                        tokens.Add(option.Value);
                    }
                }
                return tokens;
            }
        }

        public IReadOnlyList<string> ToUninstallList { get; } = Array.Empty<string>();

        public string TypeFilter => GetCommandOption("--type");

        public bool NoUpdateCheck => throw new NotImplementedException();

        public IEnumerable<string> Errors => throw new NotImplementedException();

        public string? SearchNameCriteria => string.IsNullOrWhiteSpace(TemplateName)
            ? GetCommandOption("--search")
            : TemplateName;

        public string? ListNameCriteria => string.IsNullOrWhiteSpace(TemplateName)
            ? GetCommandOption("--list")
            : TemplateName;

        public MockNewCommandInput WithTemplateOption(string optionName, string? optionValue = null)
        {
            if (!optionName.StartsWith('-'))
            {
                optionName = "--" + optionName;
            }
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

        public MockNewCommandInput WithCommandOption(string optionName, string? optionValue = null)
        {
            _commandOptions[optionName] = optionValue;
            return this;
        }

        public bool ValidateParseError() => throw new NotImplementedException();

        public bool HasDebuggingFlag(string flag)
        {
            throw new NotImplementedException();
        }

        private string GetCommandOption(string name)
        {
            return _commandOptions.ContainsKey(name) ? _commandOptions[name] ?? string.Empty : string.Empty;
        }

        #region IXunitSerializable implementation

        public void Deserialize(IXunitSerializationInfo info)
        {
            TemplateName = info.GetValue<string>("command_templateName");
            _commandOptions = JsonConvert.DeserializeObject<Dictionary<string, string?>>(info.GetValue<string>("command_options"));
            _templateOptions = JsonConvert.DeserializeObject<Dictionary<string, string?>>(info.GetValue<string>("command_templateOptions"));
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
