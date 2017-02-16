// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
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
        // Canonical form -> data type
        private IDictionary<string, string> _templateParamDataTypeMapping;
        // Maps the canonical param to the actual input param format
        private IDictionary<string, string> _templateCanonicalToInputFormatMapping;

        // stores the parsed values
        private IDictionary<string, string> _parsedTemplateParams;
        private IDictionary<string, IList<string>> _parsedInternalParams;
        private IDictionary<string, IList<string>> _parsedRemainingParams;

        // must be reset to null
        private IDictionary<string, IList<string>> _canonicalToVariantsTemplateParamMap;

        // stores the options & arguments that are NOT hidden
        // this is used exclusively to show the help.
        // it's a bit of a hack.
        CommandLineApplication _helpDisplayer;

        public ExtendedCommandParser()
        {
            _app = new CommandLineApplication(false);
            Reset();
        }

        public void Reset()
        {
            _defaultCommandOptions = new HashSet<string>();
            _hiddenCommandOptions = new Dictionary<string, CommandOptionType>();
            _hiddenCommandCanonicalMapping = new Dictionary<string, string>();
            _templateParamCanonicalMapping = new Dictionary<string, string>();
            _templateParamDataTypeMapping = new Dictionary<string, string>();
            _templateCanonicalToInputFormatMapping = new Dictionary<string, string>();

            _parsedTemplateParams = new Dictionary<string, string>();
            _parsedInternalParams = new Dictionary<string, IList<string>>();
            _parsedRemainingParams = new Dictionary<string, IList<string>>();

            _helpDisplayer = new CommandLineApplication(false);

            _canonicalToVariantsTemplateParamMap = null;
        }

        // TODO: consider optionally showing help for things not handled by the CommandLineApplication instance
        public void ShowHelp()
        {
            _helpDisplayer.ShowHelp();
        }

        public string GetOptionsHelp()
        {
            StringBuilder optionsBuilder = new StringBuilder();

            if (_helpDisplayer.Options.Any())
            {
                var maxOptLen = 0;
                foreach (var opt in _helpDisplayer.Options)
                {
                    maxOptLen = opt.Template.Length > maxOptLen ? opt.Template.Length : maxOptLen;
                }

                var outputFormat = string.Format("  {{0, -{0}}}{{1}}", maxOptLen + 2);
                foreach (var opt in _helpDisplayer.Options)
                {
                    optionsBuilder.AppendFormat(outputFormat, opt.Template, opt.Description);
                    optionsBuilder.AppendLine();
                }
            }

            return optionsBuilder.ToString();
        }

        internal void RemoveOption(CommandOption option)
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

        public void OnExecute(Func<Task<CreationResultStatus>> invoke)
        {
            _app.OnExecute(async () => (int) await invoke().ConfigureAwait(false));
        }

        // Returns the "standard" args that were input - the ones handled by the CommandLineApplication
        public List<string> RemainingArguments
        {
            get { return _app.RemainingArguments; }
        }

        internal CommandArgument Argument(string parameter, string description)
        {
            if (IsParameterNameTaken(parameter))
            {
                throw new CommandParserException($"Parameter name {parameter} cannot be used for multiple purposes", parameter);
            }

            _defaultCommandOptions.Add(parameter);

            // its not hidden, add it to the help
            _helpDisplayer.Argument(parameter, description);

            return _app.Argument(parameter, description);
        }

        internal void InternalOption(string parameterVariants, string canonical, string description, CommandOptionType optionType)
        {
            _helpDisplayer.Option(parameterVariants, description, optionType);
            HiddenInternalOption(parameterVariants, canonical, optionType);
        }

        // NOTE: the exceptions here should never happen, this is strictly called by the program
        // Once testing is done, we can probably remove them.
        internal void HiddenInternalOption(string parameterVariants, string canonical, CommandOptionType optionType)
        {
            string[] parameters = parameterVariants.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parameters.Length; i++)
            {
                if (IsParameterNameTaken(parameters[i]))
                {
                    throw new CommandParserException($"Parameter name {parameters[i]} cannot be used for multiple purposes", parameters[i]);
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

        public string TemplateParamInputFormat(string canonicalName)
        {
            _templateCanonicalToInputFormatMapping.TryGetValue(canonicalName, out string inputName);
            return inputName;
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
            _templateCanonicalToInputFormatMapping = new Dictionary<string, string>();

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
                            throw new CommandParserException($"Multiple values specified for single value parameter: {canonicalName}", canonicalName);
                        }
                    }
                    else // NoValue
                    {
                        if (param.Value.Count != 1 || param.Value[0] != null)
                        {
                            throw new CommandParserException($"Value specified for valueless parameter: {canonicalName}", canonicalName);
                        }
                    }

                    _parsedInternalParams.Add(canonicalName, param.Value);
                }
                else if (_templateParamCanonicalMapping.TryGetValue(param.Key, out canonicalName))
                {
                    if (_parsedTemplateParams.ContainsKey(canonicalName))
                    {
                        // error, the same param was specified twice
                        throw new CommandParserException($"Parameter [{canonicalName}] was specified multiple times, including with the flag [{param.Key}]", canonicalName);
                    }
                    else
                    {
                        if ((param.Value[0] == null) && (_templateParamDataTypeMapping[canonicalName] != "bool"))
                        {
                            throw new CommandParserException($"Parameter [{param.Key}] ({canonicalName}) must be given a value", canonicalName);
                        }

                        // TODO: allow for multi-valued params
                        _parsedTemplateParams[canonicalName] = param.Value[0];
                        _templateCanonicalToInputFormatMapping[canonicalName] = param.Key;
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
        // allParameters: name -> dataType
        public void SetupTemplateParameters(IEnumerable<KeyValuePair<string, string>> allParameters, IReadOnlyDictionary<string, string> longNameOverrides, IReadOnlyDictionary<string, string> shortNameOverrides)
        {
            HashSet<string> invalidParams = new HashSet<string>();

            foreach (KeyValuePair<string, string> parameter in allParameters)
            {
                string parameterName = parameter.Key;

                if (parameterName.IndexOf(':') >= 0)
                {   // Colon is reserved, template param names cannot have any.
                    invalidParams.Add(parameterName);
                    continue;
                }

                if (longNameOverrides == null || !longNameOverrides.TryGetValue(parameterName, out string flagFullText))
                {
                    flagFullText = parameterName;
                }

                bool longNameFound = false;
                bool shortNameFound = false;

                // always unless taken
                string nameAsParameter = "--" + flagFullText;
                if (!IsParameterNameTaken(nameAsParameter))
                {
                    MapTemplateParamToCanonical(nameAsParameter, parameterName);
                    longNameFound = true;
                }

                // only as fallback
                string qualifiedName = "--param:" + flagFullText;
                if (!longNameFound && !IsParameterNameTaken(qualifiedName))
                {
                    MapTemplateParamToCanonical(qualifiedName, parameterName);
                    longNameFound = true;
                }

                if (shortNameOverrides != null && shortNameOverrides.TryGetValue(parameterName, out string shortNameOverride))
                {   // short name starting point was explicitly specified
                    string fullShortNameOverride = "-" + shortNameOverride;
                    if (!IsParameterNameTaken(shortNameOverride))
                    {
                        MapTemplateParamToCanonical(fullShortNameOverride, parameterName);
                        shortNameFound = true;
                    }

                    string qualifiedShortNameOverride = "-p:" + shortNameOverride;
                    if (!shortNameFound && !IsParameterNameTaken(qualifiedShortNameOverride))
                    {
                        MapTemplateParamToCanonical(qualifiedShortNameOverride, parameterName);
                        shortNameFound = true;
                    }
                }
                else
                {   // no explicit short name specification, try generating one

                    // always unless taken
                    string shortName = GetFreeShortName(flagFullText);
                    if (!IsParameterNameTaken(shortName))
                    {
                        MapTemplateParamToCanonical(shortName, parameterName);
                        shortNameFound = true;
                    }

                    // only as fallback
                    string qualifiedShortName = GetFreeShortName(flagFullText, "p:");
                    if (!shortNameFound && !IsParameterNameTaken(qualifiedShortName))
                    {
                        MapTemplateParamToCanonical(qualifiedShortName, parameterName);
                        shortNameFound = true;
                    }
                }

                if (!shortNameFound && !longNameFound)
                {
                    invalidParams.Add(flagFullText);
                }
                else
                {
                    _templateParamDataTypeMapping[parameterName] = parameter.Value;
                }
            }

            if (invalidParams.Count > 0)
            {
                string unusableDisplayList = string.Join(", ", invalidParams);
                throw new Exception($"Template is malformed. The following parameter names are invalid: {unusableDisplayList}");
            }
        }

        private string GetFreeShortName(string name, string prefix = "")
        {
            string[] parts = name.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            string[] buckets = new string[parts.Length];

            for (int i = 0; i < buckets.Length; ++i)
            {
                buckets[i] = parts[i].Substring(0, 1);
            }

            int lastBucket = parts.Length - 1;
            while (IsParameterNameTaken("-" + prefix + string.Join("", buckets)))
            {
                //Find the next thing we can take a character from
                bool first = true;
                int end = (lastBucket + 1) % parts.Length;
                int i = (lastBucket + 1) % parts.Length;
                for (; first || i != end; first = false, i = (i + 1) % parts.Length)
                {
                    if (parts[i].Length > buckets[i].Length)
                    {
                        buckets[i] = parts[i].Substring(0, buckets[i].Length + 1);
                        break;
                    }
                }

                if (i == end)
                {
                    break;
                }
            }

            return "-" + prefix + string.Join("", buckets);
        }

        private void MapTemplateParamToCanonical(string variant, string canonical)
        {
            if (_templateParamCanonicalMapping.TryGetValue(variant, out string existingCanonical))
            {
                throw new CommandParserException($"Option variant {variant} for canonical {canonical} was already defined for canonical {existingCanonical}", canonical);
            }

            _templateParamCanonicalMapping[variant] = canonical;
        }

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
                        if (!_canonicalToVariantsTemplateParamMap.TryGetValue(canonical, out IList<string> variantList))
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

        public bool TryGetCanonicalNameForVariant(string variant, out string canonical)
        {
            return _templateParamCanonicalMapping.TryGetValue(variant, out canonical);
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
                _helpDisplayer.Name = value;
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
                _helpDisplayer.FullName = value;
            }
        }
    }
}