using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace dotnet_new3
{
    public class ExtendedCommandParser
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

        public ExtendedCommandParser(bool throwOnUnknownArg)
        {
            _app = new CommandLineApplication(throwOnUnknownArg);
            _defaultCommandOptions = new HashSet<string>();
            _hiddenCommandOptions = new Dictionary<string, CommandOptionType>();
            _hiddenCommandCanonicalMapping = new Dictionary<string, string>();
            _templateParamCanonicalMapping = new Dictionary<string, string>();
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
                throw new Exception($"Parameter name ${parameter} cannot be used for multiple purposes");
            }

            _defaultCommandOptions.Add(parameter);

            return _app.Argument(parameter, description);
        }

        public CommandOption Option(string parameterVariants, string description, CommandOptionType option)
        {
            string[] parameters = parameterVariants.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parameters.Length; i++)
            {
                if (IsParameterNameTaken(parameters[i]))
                {
                    throw new Exception($"Parameter name ${parameters[i]} cannot be used for multiple purposes");
                }

                _defaultCommandOptions.Add(parameters[i]);
            }

            return _app.Option(parameterVariants, description, option);
        }

        // NOTE: the exceptions here should never happen, this is strictly called by the program
        // Once testing is done, we can probably remove them.
        public void RegisterHiddenOption(string parameterVariants, string canonical, CommandOptionType option)
        {
            string[] parameters = parameterVariants.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parameters.Length; i++)
            {
                if (IsParameterNameTaken(parameters[i]))
                {
                    throw new Exception($"Parameter name ${parameters[i]} cannot be used for multiple purposes");
                }

                _hiddenCommandCanonicalMapping.Add(parameters[i], canonical);
            }

            _hiddenCommandOptions.Add(canonical, option);
        }

        // This must be called before trying to access the template params or internal params
        // Otherwise an exception is thrown.
        //
        // TODO: Instead of having out params, have this setup corresponding properties.
        //      It'd be more like how CommandLineApplication does things.
        public void ParseExtraArgs(IList<string> extraArgsFileNames, out IReadOnlyDictionary<string, string> templateParameters, out IReadOnlyDictionary<string, IList<string>> internalParameters)
        {
            Dictionary<string, string> tempTemplateParameters = new Dictionary<string, string>();
            Dictionary<string, IList<string>> tempInternalParameters = new Dictionary<string, IList<string>>();
            IReadOnlyDictionary<string, IList<string>> allParameters = _app.ParseExtraArgs(extraArgsFileNames);

            foreach (KeyValuePair<string, IList<string>> param in allParameters)
            {
                string canonicalName;
                if (_hiddenCommandCanonicalMapping.TryGetValue(param.Key, out canonicalName))
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
                            throw new Exception($"Multiple values specified for single value parameter: ${canonicalName}");
                        }
                    }
                    else // NoValue
                    {
                        if (param.Value.Count > 0)
                        {
                            throw new Exception($"Value specified for valueless parameter: ${canonicalName}");
                        }
                    }

                    tempInternalParameters.Add(canonicalName, param.Value);
                }
                else if (_templateParamCanonicalMapping.TryGetValue(param.Key, out canonicalName))
                {
                    if (tempTemplateParameters.ContainsKey(canonicalName))
                    {
                        // error, the same param was specified twice
                        throw new Exception($"Parameter [${canonicalName}] was specified multiple times, including with the flag [${param.Key}]");
                    }
                    else
                    {
                        // TODO: allow for multi-valued params
                        tempTemplateParameters[canonicalName] = param.Value[0];
                    }
                }
                else
                {
                    // not a known internal or template param.
                    // TODO: determine a better way to deal with this. As-is, the param will be ignored.
                }
            }

            internalParameters = tempInternalParameters;
            templateParameters = tempTemplateParameters;
        }

        // Canonical is the template param name without any dashes. The things mapped to it all have dashes, including the param name itself.
        public void SetupTemplateParameters(ITemplateInfo templateInfo)
        {
            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);
            IParameterSet allParams = template.Generator.GetParametersForTemplate(template);
            HashSet<string> invalidParams = new HashSet<string>();

            foreach (ITemplateParameter parameter in allParams.ParameterDefinitions.OrderBy(x => x.Name))
            {
                if (parameter.Name.IndexOf(':') >= 0)
                {   // Colon is reserved, template param names cannot have any.
                    invalidParams.Add(parameter.Name);
                    continue;
                }

                bool longNameFound = false;
                bool shortNameFound = false;

                // always unless taken
                string nameAsParameter = "--" + parameter.Name;
                if (!IsParameterNameTaken(nameAsParameter))
                {
                    MapTemplateParamToCanonical(nameAsParameter, parameter.Name);
                    longNameFound = true;
                }

                // only as fallback
                string qualifiedName = "--param:" + parameter.Name;
                if (!longNameFound && !IsParameterNameTaken(qualifiedName))
                {
                    MapTemplateParamToCanonical(qualifiedName, parameter.Name);
                    longNameFound = true;
                }

                // always unless taken
                string shortName = "-" + PosixNameToShortName(parameter.Name);
                if (!IsParameterNameTaken(shortName))
                {
                    MapTemplateParamToCanonical(shortName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string singleLetterName = "-" + parameter.Name.Substring(0, 1);
                if (!shortNameFound && !IsParameterNameTaken(singleLetterName))
                {
                    MapTemplateParamToCanonical(singleLetterName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string qualifiedShortName = "-p:" + PosixNameToShortName(parameter.Name);
                if (!shortNameFound && !IsParameterNameTaken(qualifiedShortName))
                {
                    MapTemplateParamToCanonical(qualifiedShortName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string qualifiedSingleLetterName = "-p:" + parameter.Name.Substring(0, 1);
                if (!shortNameFound && !IsParameterNameTaken(qualifiedSingleLetterName))
                {
                    MapTemplateParamToCanonical(qualifiedSingleLetterName, parameter.Name);
                    shortNameFound = true;
                }

                if (!shortNameFound && !longNameFound)
                {
                    invalidParams.Add(parameter.Name);
                }
            }

            if (invalidParams.Count > 0)
            {
                string unusableDisplayList = string.Join(", ", invalidParams);
                throw new Exception($"Template is malformed. The following parameter names are invalid: ${unusableDisplayList}");
            }
        }

        private void MapTemplateParamToCanonical(string variant, string canonical)
        {
            if (_templateParamCanonicalMapping.TryGetValue(variant, out string existingCanonical))
            {
                throw new Exception($"Option variant {variant} for canonical {canonical} was already defined for canonical ${existingCanonical}");
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

        // TODO: consider optionally showing help for things not handled by the CommandLineApplication instance
        public void ShowHelp()
        {
            _app.ShowHelp();
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

                        IList<string> variantList;
                        if (!_canonicalToVariantsTemplateParamMap.TryGetValue(canonical, out variantList))
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
