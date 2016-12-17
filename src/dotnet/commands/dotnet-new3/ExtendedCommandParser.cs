using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.DotNet.Tools.New3
{
    internal class ExtendedCommandParser
    {
        private CommandLineApplication _app;

        // Hidden & default options. Listed here to avoid clashes with on-the-fly params from individual templates.
        private HashSet<string> _defaultCommandOptions;
        // key is the variant, value is the canonical version
        private IDictionary<string, string> _hiddenCommandCanonicalMapping;
        // key is the canonical version
        IDictionary<string, CommandOptionType> _hiddenCommandOptions;

        // maps the template param variants to the canonical forms
        private IDictionary<string, string> _templateParamCanonicalMapping;
        // Canonical form -> data type
        private IDictionary<string, string> _templateParamDataTypeMapping;

        // stores the parsed values
        private IDictionary<string, string> _parsedTemplateParams;
        private IDictionary<string, IList<string>> _parsedInternalParams;
        private IDictionary<string, IList<string>> _parsedRemainingParams;

        // stores the options & arguments that are NOT hidden
        // this is used exclusively to show the help.
        // it's a bit of a hack.
        CommandLineApplication _helpDisplayer;

        public ExtendedCommandParser()
        {
            _app = new CommandLineApplication(false);
            _defaultCommandOptions = new HashSet<string>();
            _hiddenCommandOptions = new Dictionary<string, CommandOptionType>();
            _hiddenCommandCanonicalMapping = new Dictionary<string, string>();
            _templateParamCanonicalMapping = new Dictionary<string, string>();
            _templateParamDataTypeMapping = new Dictionary<string, string>();

            _parsedTemplateParams = new Dictionary<string, string>();
            _parsedInternalParams = new Dictionary<string, IList<string>>();
            _parsedRemainingParams = new Dictionary<string, IList<string>>();

            _helpDisplayer = new CommandLineApplication(false);
        }

        // TODO: consider optionally showing help for things not handled by the CommandLineApplication instance
        public void ShowHelp()
        {
            _helpDisplayer.ShowHelp();
        }

        public void RemoveOption(CommandOption option)
        {
            _app.Options.Remove(option);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsParameterNameTaken(string testName)
        {
            return _defaultCommandOptions.Contains(testName)
                || _hiddenCommandCanonicalMapping.ContainsKey(testName)
                || _templateParamCanonicalMapping.ContainsKey(testName);
        }

        public int Execute(params string[] args)
        {
            return _app.Execute(args);
        }

        public void OnExecute(Func<Task<int>> invoke)
        {
            _app.OnExecute(invoke);
        }

        // Returns the "standard" args that were input - the ones handled by the CommandLineApplication
        public List<string> RemainingArguments
        {
            get { return _app.RemainingArguments; }
        }

        public CommandArgument Argument(string parameter, string description)
        {
            if (IsParameterNameTaken(parameter))
            {
                throw new Exception(string.Format(LocalizableStrings.ParameterReuseError, parameter));
            }

            _defaultCommandOptions.Add(parameter);

            // its not hidden, add it to the help
            _helpDisplayer.Argument(parameter, description);

            return _app.Argument(parameter, description);
        }

        public void InternalOption(string parameterVariants, string canonical, string description, CommandOptionType optionType)
        {
            _helpDisplayer.Option(parameterVariants, description, optionType);
            HiddenInternalOption(parameterVariants, canonical, optionType);
        }

        // NOTE: the exceptions here should never happen, this is strictly called by the program
        // Once testing is done, we can probably remove them.
        public void HiddenInternalOption(string parameterVariants, string canonical, CommandOptionType optionType)
        {
            string[] parameters = parameterVariants.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parameters.Length; i++)
            {
                if (IsParameterNameTaken(parameters[i]))
                {
                    throw new Exception(string.Format(LocalizableStrings.ParameterReuseError, parameters[i]));
                }

                _hiddenCommandCanonicalMapping.Add(parameters[i], canonical);
            }

            _hiddenCommandOptions.Add(canonical, optionType);
        }

        public bool TemplateParamHasValue(string paramName)
        {
            return _parsedTemplateParams.ContainsKey(paramName);
        }

        public string TemplateParamValue(string paramName)
        {
            _parsedTemplateParams.TryGetValue(paramName, out string value);
            return value;
        }

        // returns a copy of the template params
        public IReadOnlyDictionary<string, string> AllTemplateParams
        {
            get
            {
                return new Dictionary<string, string>(_parsedTemplateParams);
            }
        }

        public bool InternalParamHasValue(string paramName)
        {
            return _parsedInternalParams.ContainsKey(paramName);
        }

        public string InternalParamValue(string paramName)
        {
            if (_parsedInternalParams.TryGetValue(paramName, out IList<string> values))
            {
                return values.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        public IList<string> InternalParamValueList(string paramName)
        {
            _parsedInternalParams.TryGetValue(paramName, out IList<string> values);
            return values;
        }

        public IDictionary<string, IList<string>> RemainingParameters
        {
            get
            {
                return _parsedRemainingParams;
            }
        }

        // Parses all command line args, and any input arg files.
        // NOTE: any previously parsed values are lost - this resets the parsed values.
        public void ParseArgs(IList<string> extraArgFileNames = null)
        {
            _parsedTemplateParams = new Dictionary<string, string>();
            _parsedInternalParams = new Dictionary<string, IList<string>>();
            _parsedRemainingParams = new Dictionary<string, IList<string>>();

            if (extraArgFileNames == null)
            {
                extraArgFileNames = new List<string>();
            }
            IReadOnlyDictionary<string, IList<string>> allParameters = _app.ParseExtraArgs(extraArgFileNames);

            foreach (KeyValuePair<string, IList<string>> param in allParameters)
            {
                if (_hiddenCommandCanonicalMapping.TryGetValue(param.Key, out string canonicalName))
                {
                    CommandOptionType optionType = _hiddenCommandOptions[canonicalName];

                    if (optionType == CommandOptionType.MultipleValue)
                    {
                        ;   // nothing to check
                    }
                    else if (optionType == CommandOptionType.SingleValue)
                    {
                        if (param.Value.Count != 1)
                        {
                            throw new Exception(string.Format(LocalizableStrings.MultipleValuesSpecifiedForSingleValuedParameter, canonicalName));
                        }
                    }
                    else // NoValue
                    {
                        if (param.Value.Count != 1 || param.Value[0] != null)
                        {
                            throw new Exception(string.Format(LocalizableStrings.ValueSpecifiedForValuelessParameter, canonicalName));
                        }
                    }

                    _parsedInternalParams.Add(canonicalName, param.Value);
                }
                else if (_templateParamCanonicalMapping.TryGetValue(param.Key, out canonicalName))
                {
                    if (_parsedTemplateParams.ContainsKey(canonicalName))
                    {
                        // error, the same param was specified twice
                        throw new Exception(string.Format(LocalizableStrings.ParameterSpecifiedMultipleTimes, canonicalName, param.Key));
                    }
                    else
                    {
                        if ((param.Value[0] == null) && (_templateParamDataTypeMapping[canonicalName] != "bool"))
                        {
                            throw new Exception(string.Format(LocalizableStrings.ParameterMissingValue, param.Key, canonicalName));
                        }
                        
                        // TODO: allow for multi-valued params
                        _parsedTemplateParams[canonicalName] = param.Value[0];
                    }
                }
                else
                {
                    // not a known internal or template param.
                    _parsedRemainingParams[param.Key] = param.Value;
                }
            }
        }

        // Canonical is the template param name without any dashes. The things mapped to it all have dashes, including the param name itself.
        public void SetupTemplateParameters(IParameterSet allParams, IReadOnlyDictionary<string, string> parameterNameMap)
        {
            HashSet<string> invalidParams = new HashSet<string>();

            foreach (ITemplateParameter parameter in allParams.ParameterDefinitions.Where(x => x.Priority != TemplateParameterPriority.Implicit).OrderBy(x => x.Name))
            {
                if (parameter.Name.IndexOf(':') >= 0)
                {   // Colon is reserved, template param names cannot have any.
                    invalidParams.Add(parameter.Name);
                    continue;
                }

                if (parameterNameMap == null || !parameterNameMap.TryGetValue(parameter.Name, out string flagFullText))
                {
                    flagFullText = parameter.Name;
                }

                bool longNameFound = false;
                bool shortNameFound = false;

                // always unless taken
                string nameAsParameter = "--" + flagFullText;
                if (!IsParameterNameTaken(nameAsParameter))
                {
                    MapTemplateParamToCanonical(nameAsParameter, parameter.Name);
                    longNameFound = true;
                }

                // only as fallback
                string qualifiedName = "--param:" + flagFullText;
                if (!longNameFound && !IsParameterNameTaken(qualifiedName))
                {
                    MapTemplateParamToCanonical(qualifiedName, parameter.Name);
                    longNameFound = true;
                }

                // always unless taken
                string shortName = "-" + PosixNameToShortName(flagFullText);
                if (!IsParameterNameTaken(shortName))
                {
                    MapTemplateParamToCanonical(shortName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string singleLetterName = "-" + flagFullText.Substring(0, 1);
                if (!shortNameFound && !IsParameterNameTaken(singleLetterName))
                {
                    MapTemplateParamToCanonical(singleLetterName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string qualifiedShortName = "-p:" + PosixNameToShortName(flagFullText);
                if (!shortNameFound && !IsParameterNameTaken(qualifiedShortName))
                {
                    MapTemplateParamToCanonical(qualifiedShortName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string qualifiedSingleLetterName = "-p:" + flagFullText.Substring(0, 1);
                if (!shortNameFound && !IsParameterNameTaken(qualifiedSingleLetterName))
                {
                    MapTemplateParamToCanonical(qualifiedSingleLetterName, parameter.Name);
                    shortNameFound = true;
                }

                if (!shortNameFound && !longNameFound)
                {
                    invalidParams.Add(flagFullText);
                }
                else
                {
                    _templateParamDataTypeMapping[parameter.Name] = parameter.DataType;
                }
            }

            if (invalidParams.Count > 0)
            {
                string unusableDisplayList = string.Join(", ", invalidParams);
                throw new Exception(string.Format(LocalizableStrings.TemplateMalformedDueToBadParameters, unusableDisplayList));
            }
        }

        private void MapTemplateParamToCanonical(string variant, string canonical)
        {
            if (_templateParamCanonicalMapping.TryGetValue(variant, out string existingCanonical))
            {
                throw new Exception(string.Format(LocalizableStrings.OptionVariantAlreadyDefined, variant, canonical, existingCanonical));
            }

            _templateParamCanonicalMapping[variant] = canonical;
        }

        // Concats the first letter of dash separated word.
        private static string PosixNameToShortName(string name)
        {
            IList<string> wordsInName = name.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            IList<string> firstLetters = new List<string>();

            foreach (string word in wordsInName)
            {
                firstLetters.Add(word.Substring(0, 1));
            }

            return string.Join("", firstLetters);
        }

        private IDictionary<string, IList<string>> _canonicalToVariantsTemplateParamMap;

        public IDictionary<string, IList<string>> CanonicalToVariantsTemplateParamMap
        {
            get
            {
                if (_canonicalToVariantsTemplateParamMap == null)
                {
                    _canonicalToVariantsTemplateParamMap = new Dictionary<string, IList<string>>();

                    foreach (KeyValuePair<string, string> variantToCanonical in _templateParamCanonicalMapping)
                    {
                        string variant = variantToCanonical.Key;
                        string canonical = variantToCanonical.Value;
                        if (!_canonicalToVariantsTemplateParamMap.TryGetValue(canonical, out var variantList))
                        {
                            variantList = new List<string>();
                            _canonicalToVariantsTemplateParamMap.Add(canonical, variantList);
                        }

                        variantList.Add(variant);
                    }
                }

                return _canonicalToVariantsTemplateParamMap;
            }
        }

        public string Name
        {
            get
            {
                return _app.Name;
            }
            set
            {
                _app.Name = value;
            }
        }

        public string FullName
        {
            get
            {
                return _app.FullName;
            }
            set
            {
                _app.FullName = value;
            }
        }
    }
}
