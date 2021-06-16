// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.CommandParsing
{
    internal class TemplateCommandInput : BaseCommandInput
    {
        private IReadOnlyDictionary<string, IReadOnlyList<string>> _templateParamCanonicalToVariantMap;
        private IReadOnlyDictionary<string, string?>? _templateParamValues;
        private IReadOnlyDictionary<string, string>? _templateParamVariantToCanonicalMap;

        internal TemplateCommandInput(string commandName, ParseResult parseResult, string templateName, IReadOnlyDictionary<string, IReadOnlyList<string>> templateParamMap) : base(commandName, parseResult, templateName)
        {
            _templateParamCanonicalToVariantMap = templateParamMap;
        }

        public IReadOnlyDictionary<string, string?> InputTemplateParams
        {
            get
            {
                if (_templateParamValues == null)
                {
                    Dictionary<string, string?> templateParamValues = new Dictionary<string, string?>();

                    foreach (KeyValuePair<string, IReadOnlyList<string>> paramInfo in _templateParamCanonicalToVariantMap)
                    {
                        string paramName = paramInfo.Key;
                        string firstVariant = paramInfo.Value[0];

                        // This returns true if the arg was specified, irrespective of whether it has a value.
                        // If the arg was specified, it goes in the list.
                        // Null valued args are important - they facilitate bools & other value-optional args.
                        if (CommandParseResult.TryGetArgumentValueAtPath(out string? argValue, new[] { CommandName, firstVariant }))
                        {
                            templateParamValues.Add(paramName, argValue);
                        }
                    }
                    _templateParamValues = templateParamValues;
                }

                return _templateParamValues;
            }
        }

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

        internal static TemplateCommandInput ParseForTemplate(ITemplateInfo templateInfo, INewCommandInput commandInput, HostSpecificTemplateData hostSpecificTemplateData)
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
                            commandInput.CommandName,
                            templateInfo.ShortNameList[0],
                            filteredParams,
                            hostSpecificTemplateData.LongNameOverrides,
                            hostSpecificTemplateData.ShortNameOverrides,
                            out IReadOnlyDictionary<string, IReadOnlyList<string>> templateParamMap);

                IEnumerable<string> tokens;
                if (string.IsNullOrWhiteSpace(commandInput.TemplateName))
                {
                    tokens = commandInput.Tokens;
                }
                else
                {
                    tokens = commandInput.Tokens.Skip(1);
                }
                return ParseArgs(
                    commandInput.CommandName,
                    templateInfo.ShortNameList[0],
                    tokens,
                    templateSpecificCommand,
                    templateParamMap);
            }
            catch (Exception ex)
            {
                throw new CommandParserException($"Failed to reparse command for template {templateInfo.Identity}", string.Join(" ", commandInput.Tokens), ex);
            }
        }

        internal IReadOnlyList<string> VariantsForCanonical(string canonical)
        {
            if (_templateParamCanonicalToVariantMap == null || !_templateParamCanonicalToVariantMap.TryGetValue(canonical, out IReadOnlyList<string>? variants))
            {
                return new List<string>();
            }
            return variants;
        }

        internal string TemplateParamInputFormat(string canonical)
        {
            foreach (string variant in VariantsForCanonical(canonical))
            {
                if (CommandParseResult.Tokens.Contains(variant))
                {
                    return variant;
                }
            }

            // in case parameter is specified as --aaa=bbb, Tokens collection contains --aaa=bbb as single token
            // in this case we need to check if token starts with variant=
            foreach (string variant in VariantsForCanonical(canonical))
            {
                if (CommandParseResult.Tokens.Any(s => s.StartsWith($"{variant}=")))
                {
                    return variant;
                }
            }

            // this is really an error. But returning the canonical is "safe"
            return canonical;
        }

        internal bool TryGetCanonicalNameForVariant(string variant, out string? canonical)
        {
            return TemplateParamVariantToCanonicalMap.TryGetValue(variant, out canonical);
        }

        private static TemplateCommandInput ParseArgs(
            string commandName,
            string templateShortName,
            IEnumerable<string> args,
            Command command,
            IReadOnlyDictionary<string, IReadOnlyList<string>> templateParamMap)
        {
            List<string> argsWithCommand = new List<string>() { commandName, templateShortName };
            argsWithCommand.AddRange(args);
            ParserConfiguration parseConfig = new ParserConfiguration(new Option[] { command }, argumentDelimiters: new[] { '=' }, allowUnbundling: false);
            Parser parser = new Parser(parseConfig);
            var parseResult = parser.Parse(argsWithCommand.ToArray());

            return new TemplateCommandInput(commandName, parseResult, templateShortName, templateParamMap);
        }
    }
}
