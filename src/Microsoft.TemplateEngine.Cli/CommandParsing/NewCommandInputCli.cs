// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    internal class NewCommandInputCli : INewCommandInput
    {
        internal const string AuthorColumnFilter = "author";
        internal const string LanguageColumnFilter = "language";
        internal const string TagsColumnFilter = "tags";
        internal const string TypeColumnFilter = "type";

        internal static readonly string[] SupportedFilterableColumnNames = new[]
        {
            AuthorColumnFilter,
            TypeColumnFilter,
            LanguageColumnFilter,
            TagsColumnFilter
        };
        private readonly string _commandName;
        // used for parsing args outside the context of a specific template.
        private readonly Command _noTemplateCommand;

        private IReadOnlyList<string> _args;
        private Command _currentCommand;
        private Func<Task<int>> _invoke;
        private ParseResult _parseResult;
        private string _templateNameArg;
        private IReadOnlyDictionary<string, IReadOnlyList<string>> _templateParamCanonicalToVariantMap;
        private IReadOnlyDictionary<string, string> _templateParamValues;
        private IReadOnlyDictionary<string, string> _templateParamVariantToCanonicalMap;

        internal NewCommandInputCli(string commandName)
        {
            _commandName = commandName;
            _noTemplateCommand = CommandParserSupport.CreateNewCommandWithoutTemplateInfo(_commandName);
            _currentCommand = _noTemplateCommand;
            ExpandedExtraArgsFiles = false;
        }

        public string Alias => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "alias" });

        public string AllowScriptsToRun
        {
            get
            {
                if (_parseResult.TryGetArgumentValueAtPath(out string argValue, new[] { _commandName, "allow-scripts" }))
                {
                    return argValue;
                }

                return null;
            }
        }

        public bool ApplyUpdates => _parseResult.HasAppliedOption(new[] { _commandName, "update-apply" });
        public string AuthorFilter => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "author" });

        public string BaselineName => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "baseline" });

        public bool CheckForUpdates => _parseResult.HasAppliedOption(new[] { _commandName, "update-check" });
        public IReadOnlyCollection<string> Columns
        {
            get
            {
                string columnNames = _parseResult.GetArgumentValueAtPath(new[] { _commandName, "columns" });
                if (!string.IsNullOrWhiteSpace(columnNames))
                {
                    return columnNames.Split(',').Select(s => s.Trim()).ToList();
                }
                else
                {
                    return new List<string>();
                }
            }
        }

        public string ColumnsParseError
        {
            get
            {
                List<string> invalidColumns = new List<string>(Columns.Count);
                foreach (string columnToShow in Columns)
                {
                    if (!SupportedFilterableColumnNames.Contains(columnToShow))
                    {
                        invalidColumns.Add(columnToShow);
                    }
                }
                if (invalidColumns.Any())
                {
                    return string.Format(
                         LocalizableStrings.ColumnNamesAreNotSupported,
                         string.Join(", ", invalidColumns.Select(s => $"'{s}'")),
                         string.Join(", ", SupportedFilterableColumnNames.Select(s => $"'{s}'")));
                }
                return string.Empty;
            }
        }

        public string CommandName => _commandName;

        public bool ExpandedExtraArgsFiles { get; private set; }

        public IList<string> ExtraArgsFileNames => _parseResult.GetArgumentListAtPath(new[] { _commandName, "extra-args" })?.ToList();

        public bool HasColumnsParseError => Columns.Any(column => !SupportedFilterableColumnNames.Contains(column));

        public bool HasParseError
        {
            get
            {
                return _parseResult.Errors.Any() || HasColumnsParseError;
            }
        }

        public string HelpText
        {
            get
            {
                return _noTemplateCommand.HelpView();
            }
        }

        public IReadOnlyDictionary<string, string> InputTemplateParams
        {
            get
            {
                if (_templateParamValues == null)
                {
                    _templateParamValues = new Dictionary<string, string>();
                }

                return _templateParamValues;
            }
        }

        public IList<string> InstallNuGetSourceList => _parseResult.GetArgumentListAtPath(new[] { _commandName, "nuget-source" })?.ToList();

        public bool IsDryRun => _parseResult.HasAppliedOption(new[] { _commandName, "dry-run" });

        public bool IsForceFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "force" });

        public bool IsHelpFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "help" });

        public bool IsInteractiveFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "interactive" });

        public bool IsListFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "list" });

        public bool IsQuietFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "quiet" });

        public bool IsShowAllFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "all" });

        public string Language => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "language" });

        public string Name => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "name" });

        public string OutputPath => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "output" });

        public string PackageFilter => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "package" });

        public List<string> RemainingArguments
        {
            get
            {
                return _parseResult.UnmatchedTokens.ToList();
            }
        }

        public IDictionary<string, IList<string>> RemainingParameters
        {
            get
            {
                Dictionary<string, IList<string>> remainingParameters = new Dictionary<string, IList<string>>();

                foreach (string param in _parseResult.UnmatchedTokens)
                {
                    remainingParameters[param] = new List<string>();
                }

                return remainingParameters;
            }
        }

        public bool SearchOnline => _parseResult.HasAppliedOption(new[] { _commandName, "search" });

        public string ShowAliasesAliasName => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "show-alias" });

        public bool ShowAliasesSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "show-alias" });

        public bool ShowAllColumns => _parseResult.HasAppliedOption(new[] { _commandName, "columns-all" });

        public bool SkipUpdateCheck => _parseResult.HasAppliedOption(new[] { _commandName, "skip-update-check" });

        public string TagFilter => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "tag" });

        public string TemplateName => _templateNameArg;

        public IList<string> ToInstallList => _parseResult.GetArgumentListAtPath(new[] { _commandName, "install" })?.ToList();

        public IReadOnlyList<string> Tokens
        {
            get
            {
                if (_parseResult == null)
                {
                    return new List<string>();
                }

                return _parseResult.Tokens.ToList();
            }
        }

        public IList<string> ToUninstallList => _parseResult.GetArgumentListAtPath(new[] { _commandName, "uninstall" })?.ToList();

        public string TypeFilter => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "type" });

        // Maps the template-related flag variants to their canonical
        private IReadOnlyDictionary<string, string> TemplateParamVariantToCanonicalMap
        {
            get
            {
                if (_templateParamVariantToCanonicalMap == null)
                {
                    Dictionary<string, string> map = new Dictionary<string, string>();

                    if (_templateParamCanonicalToVariantMap != null)
                    {
                        foreach (KeyValuePair<string, IReadOnlyList<string>> canonicalToVariants in _templateParamCanonicalToVariantMap)
                        {
                            string canonical = canonicalToVariants.Key;

                            foreach (string variant in canonicalToVariants.Value)
                            {
                                map.Add(variant, canonical);
                            }
                        }
                    }

                    _templateParamVariantToCanonicalMap = map;
                }

                return _templateParamVariantToCanonicalMap;
            }

        }

        public int Execute(params string[] args)
        {
            _args = args;
            ParseArgs();
            bool needsReparse = false;

            if (ExtraArgsFileNames != null && ExtraArgsFileNames.Count > 0)
            {
                // add the extra args to the _args and force a reparse
                // This cannot adjust the template name, so no need to re-check here.
                IReadOnlyList<string> extraArgs = AppExtensions.CreateArgListFromAdditionalFiles(ExtraArgsFileNames);
                List<string> allArgs = RemoveExtraArgsTokens(_args);
                allArgs.AddRange(extraArgs);
                _args = allArgs;
                needsReparse = true;
                ExpandedExtraArgsFiles = true;
            }

            if (string.IsNullOrEmpty(_templateNameArg))
            {
                _currentCommand = CommandParserSupport.CreateNewCommandForNoTemplateName(_commandName);
                needsReparse = true;
            }

            if (needsReparse)
            {
                ParseArgs();
            }

            return _invoke.Invoke().Result;
        }

        public bool HasDebuggingFlag(string flag)
        {
            return _parseResult.HasAppliedOption(new[] { _commandName, flag });
        }

        public void OnExecute(Func<Task<CreationResultStatus>> invoke)
        {
            _invoke = async () => (int)await invoke().ConfigureAwait(false);
        }

        public void ReparseForTemplate(ITemplateInfo templateInfo, HostSpecificTemplateData hostSpecificTemplateData)
        {
            // The params getting filtered out are "standard" to dotnet new - they get explicitly setup in the command
            //      and their flags cannot be overridden by host specific configuration.
            // type & language: These are "tags" in template.json, which become params in the templateInfo object.
            // name: Gets added as a param in SimpleConfigModel - to facilitate the built in value forms for name.
            //       name can also be explicitly specified in the template.json - for custom value forms on name.
            List<ITemplateParameter> filteredParams = templateInfo.Parameters.Where(x => !string.Equals(x.Name, "type", StringComparison.OrdinalIgnoreCase)
                                                                                    && !string.Equals(x.Name, "language", StringComparison.OrdinalIgnoreCase)
                                                                                    && !string.Equals(x.Name, "name", StringComparison.OrdinalIgnoreCase))
                                                                                    .ToList();
            Command templateSpecificCommand;

            try
            {
                templateSpecificCommand = CommandParserSupport.CreateNewCommandWithArgsForTemplate(
                            _commandName,
                            _templateNameArg,
                            filteredParams,
                            hostSpecificTemplateData.LongNameOverrides,
                            hostSpecificTemplateData.ShortNameOverrides,
                            out IReadOnlyDictionary<string, IReadOnlyList<string>> templateParamMap);

                _currentCommand = templateSpecificCommand;
                ParseArgs();

                // this must happen after ParseArgs(), which resets _templateParamCanonicalToVariantMap
                _templateParamCanonicalToVariantMap = templateParamMap;

                Dictionary<string, string> templateParamValues = new Dictionary<string, string>();

                foreach (KeyValuePair<string, IReadOnlyList<string>> paramInfo in _templateParamCanonicalToVariantMap)
                {
                    string paramName = paramInfo.Key;
                    string firstVariant = paramInfo.Value[0];

                    // This returns true if the arg was specified, irrespective of whether it has a value.
                    // If the arg was specified, it goes in the list.
                    // Null valued args are important - they facilitate bools & other value-optional args.
                    if (_parseResult.TryGetArgumentValueAtPath(out string argValue, new[] { _commandName, firstVariant }))
                    {
                        templateParamValues.Add(paramName, argValue);
                    }
                }

                _templateParamValues = templateParamValues;
            }
            catch (Exception ex)
            {
                throw new CommandParserException("Error parsing input parameters", string.Join(" ", _args), ex);
            }
        }

        public void ResetArgs(params string[] args)
        {
            _args = args;
            ExpandedExtraArgsFiles = false;
            ParseArgs();
        }
        // TODO: probably deprecate one of RemainingArguments | RemainingParameters
        public bool TemplateParamHasValue(string paramName)
        {
            return InputTemplateParams.ContainsKey(paramName);
        }

        public string TemplateParamInputFormat(string canonical)
        {
            foreach (string variant in VariantsForCanonical(canonical))
            {
                if (_parseResult.Tokens.Contains(variant))
                {
                    return variant;
                }
            }

            // in case parameter is specified as --aaa=bbb, Tokens collection contains --aaa=bbb as single token
            // in this case we need to check if token starts with variant=
            foreach (string variant in VariantsForCanonical(canonical))
            {
                if (_parseResult.Tokens.Any(s => s.StartsWith($"{variant}=")))
                {
                    return variant;
                }
            }

            // this is really an error. But returning the canonical is "safe"
            return canonical;
        }

        public string TemplateParamValue(string paramName)
        {
            InputTemplateParams.TryGetValue(paramName, out string value);
            return value;
        }

        public bool TryGetCanonicalNameForVariant(string variant, out string canonical)
        {
            return TemplateParamVariantToCanonicalMap.TryGetValue(variant, out canonical);
        }

        public IReadOnlyList<string> VariantsForCanonical(string canonical)
        {
            if (_templateParamCanonicalToVariantMap == null || !_templateParamCanonicalToVariantMap.TryGetValue(canonical, out IReadOnlyList<string> variants))
            {
                return new List<string>();
            }

            return variants;
        }

        private void ParseArgs()
        {
            List<string> argsWithCommand = new List<string>() { _commandName };
            argsWithCommand.AddRange(_args);
            ParserConfiguration parseConfig = new ParserConfiguration(new Option[] { _currentCommand }, argumentDelimiters: new[] { '=' }, allowUnbundling: false);
            Parser parser = new Parser(parseConfig);
            _parseResult = parser.Parse(argsWithCommand.ToArray());
            _templateParamCanonicalToVariantMap = null;
            _templateParamVariantToCanonicalMap = null;

            IReadOnlyList<string> templateNameList = _parseResult.GetArgumentListAtPath(new[] { _commandName })?.ToList() ?? Empty<string>.List.Value;
            if ((templateNameList.Count > 0) &&
                !templateNameList[0].StartsWith("-", StringComparison.Ordinal)
                && (_parseResult.Tokens.Count >= 2)
                && string.Equals(templateNameList[0], _parseResult.Tokens.ElementAt(1), StringComparison.Ordinal))
            {
                _templateNameArg = templateNameList[0];
            }
            else
            {
                _templateNameArg = string.Empty;
            }
        }

        private List<string> RemoveExtraArgsTokens(IReadOnlyList<string> originalArgs)
        {
            List<string> modifiedArgs = new List<string>();
            bool inExtraArgsContext = false;

            foreach (string token in originalArgs)
            {
                if (string.Equals(token, "-x", StringComparison.Ordinal) || string.Equals(token, "--extra-args", StringComparison.Ordinal))
                {
                   inExtraArgsContext = true;
                }
                else if (inExtraArgsContext && ExtraArgsFileNames.Contains(token, StringComparer.Ordinal))
                {
                    // Do nothing (there can be multiple extra args files).
                    // inExtraArgsContext guards against the slim possibility of a different arg having the same value as an args filename.
                    // There can be multiple extra args files - finding one doesn't change the state of things.
                }
                else
                {
                    modifiedArgs.Add(token);
                    inExtraArgsContext = false;
                }
            }

            return modifiedArgs;
        }
    }
}
