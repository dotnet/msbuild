using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    public class NewCommandInputCli : INewCommandInput
    {
        private ParseResult _parseResult;
        private IReadOnlyList<string> _args;
        private string _templateNameArg;

        private Command _currentCommand;
        private Func<Task<int>> _invoke;

        private IReadOnlyDictionary<string, string> _templateParamValues;
        private IReadOnlyDictionary<string, IReadOnlyList<string>> _templateParamCanonicalToVariantMap;

        // used for parsing args outside the context of a specific template.
        private readonly Command _noTemplateCommand;
        private readonly string _commandName;

        public NewCommandInputCli(string commandName)
        {
            _commandName = commandName;
            _noTemplateCommand = CommandParserSupport.CreateNewCommandWithoutTemplateInfo(_commandName);
            _currentCommand = _noTemplateCommand;
            ExpandedExtraArgsFiles = false;
        }

        public bool HasParseError
        {
            get
            {
                return _parseResult.Errors.Any();
            }
        }

        public void ResetArgs(params string[] args)
        {
            _args = args;
            ExpandedExtraArgsFiles = false;
            ParseArgs();
        }

        public bool ExpandedExtraArgsFiles { get; private set; }

        public int Execute(params string[] args)
        {
            _args = args;
            ParseArgs();
            bool needsReparse = false;

            if (ExtraArgsFileNames != null && ExtraArgsFileNames.Count > 0)
            {   // add the extra args to the _args and force a reparse
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

        public void OnExecute(Func<Task<CreationResultStatus>> invoke)
        {
            _invoke = async () => (int)await invoke().ConfigureAwait(false);
        }

        private void ParseArgs()
        {
            List<string> argsWithCommand = new List<string>() { _commandName };
            argsWithCommand.AddRange(_args);
            ParserConfiguration parseConfig = new ParserConfiguration(new Option[] { _currentCommand }, argumentDelimiters: new[] { '=' }, allowUnbundling: false);
            Parser parser = new Parser(parseConfig);
            _parseResult = parser.Parse(argsWithCommand.ToArray());
            _templateParamCanonicalToVariantMap = null;

            IList<string> templateNameList = _parseResult.GetArgumentListAtPath(new[] { _commandName }).ToList();
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

        public void ReparseForTemplate(ITemplateInfo templateInfo, HostSpecificTemplateData hostSpecificTemplateData)
        {
            List<ITemplateParameter> filteredParams = templateInfo.Parameters.Where(x => !string.Equals(x.Name, "type", StringComparison.OrdinalIgnoreCase) 
                                                                                    && !string.Equals(x.Name, "language", StringComparison.OrdinalIgnoreCase))
                                                                                    .ToList();
            Command _templateSpecificCommand;

            try
            {
                _templateSpecificCommand = CommandParserSupport.CreateNewCommandWithArgsForTemplate(
                            _commandName,
                            _templateNameArg,
                            filteredParams,
                            hostSpecificTemplateData.LongNameOverrides,
                            hostSpecificTemplateData.ShortNameOverrides,
                            out IReadOnlyDictionary<string, IReadOnlyList<string>> templateParamMap);

                _currentCommand = _templateSpecificCommand;
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

        public string TemplateName => _templateNameArg;

        public string Alias => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "alias" });

        public bool ShowAliasesSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "show-alias" });

        public string ShowAliasesAliasName => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "show-alias" });

        public string BaselineName => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "baseline" });

        public IList<string> ExtraArgsFileNames => _parseResult.GetArgumentListAtPath(new[] { _commandName, "extra-args" }).ToList();

        public IList<string> ToInstallList => _parseResult.GetArgumentListAtPath(new[] { _commandName, "install" }).ToList();

        public IList<string> ToUninstallList => _parseResult.GetArgumentListAtPath(new[] { _commandName, "uninstall" }).ToList();

        public bool IsForceFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "force" });

        public bool IsHelpFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "help" });

        public bool IsListFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "list" });

        public bool IsQuietFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "quiet" });

        public bool IsShowAllFlagSpecified => _parseResult.HasAppliedOption(new[] { _commandName, "all" });

        public string TypeFilter => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "type" });

        public string Language => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "language" });

        public string Locale => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "locale" });

        public string Name => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "name" });

        public string OutputPath => _parseResult.GetArgumentValueAtPath(new[] { _commandName, "output" });

        public bool SkipUpdateCheck => _parseResult.HasAppliedOption(new[] { _commandName, "skip-update-check" });

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

        public bool HasDebuggingFlag(string flag)
        {
            return _parseResult.HasAppliedOption(new[] { _commandName, flag });
        }

        public IReadOnlyDictionary<string, string> AllTemplateParams
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

        public string TemplateParamInputFormat(string canonical)
        {
            foreach (string variant in VariantsForCanonical(canonical))
            {
                if (_parseResult.Tokens.Contains(variant))
                {
                    return variant;
                }
            }

            // this is really an error. But returning the canonical is "safe"
            return canonical;
        }

        public IReadOnlyList<string> VariantsForCanonical(string canonical)
        {
            if (_templateParamCanonicalToVariantMap == null || !_templateParamCanonicalToVariantMap.TryGetValue(canonical, out IReadOnlyList<string> variants))
            {
                return new List<string>();
            }

            return variants;
        }

        public bool TryGetCanonicalNameForVariant(string variant, out string canonical)
        {
            return TemplateParamVariantToCanonicalMap.TryGetValue(variant, out canonical);
        }

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
        private IReadOnlyDictionary<string, string> _templateParamVariantToCanonicalMap;

        public List<string> RemainingArguments
        {
            get
            {
                return _parseResult.UnmatchedTokens.ToList();
            }
        }

        // TODO: probably deprecate one of RemainingArguments | RemainingParameters

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

        public bool TemplateParamHasValue(string paramName)
        {
            return AllTemplateParams.ContainsKey(paramName);
        }

        public string TemplateParamValue(string paramName)
        {
            AllTemplateParams.TryGetValue(paramName, out string value);
            return value;
        }

        public string HelpText
        {
            get
            {
                return _noTemplateCommand.HelpView();
            }
        }
    }
}
