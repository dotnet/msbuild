// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateGroupArgs
    {
        private static Option<string> _outputOption = new Option<string>("-o");
        private static Option<string> _nameOption = new Option<string>("-n");
        private static Option<bool> _helpOption = new Option<bool>(new string[] { "-h", "--help", "-?" });

        public TemplateGroupArgs(ITemplateInfo template, ParseResult parseResult, Dictionary<string, Option> templateSpecificOptions)
        {
            Name = parseResult.GetValueForOption(_nameOption);
            OutputPath = parseResult.GetValueForOption(_outputOption);
            foreach (var opt in templateSpecificOptions)
            {
                TemplateSpecificOptions[opt.Key] = parseResult.GetValueForOption(opt.Value)?.ToString();
            }
            Template = template;
            HelpRequested = parseResult.GetValueForOption(_helpOption);
        }

        public Dictionary<string, string?> TemplateSpecificOptions { get; } = new();

        public string? Name { get; }

        public string? OutputPath { get; }

        public bool IsForceFlagSpecified { get; internal set; }

        public string? BaselineName { get; internal set; }

        public bool IsDryRun { get; internal set; }

        public bool HelpRequested { get; internal set; }

        public ITemplateInfo Template { get; }

        internal static Dictionary<string, Option> AddToCommand(TemplateGroupCommand templateGroupCommand, ITemplateInfo template)
        {
            templateGroupCommand.AddOption(_outputOption);
            templateGroupCommand.AddOption(_nameOption);
            templateGroupCommand.AddOption(_helpOption);
            var templateSpecificOptions = new Dictionary<string, Option>();

            foreach (var p in template.Parameters)
            {
                if (p.Priority == TemplateParameterPriority.Implicit)
                {
                    continue;
                }

                Option? opt = null;
                string optionName = $"--{p.Name}";
                switch (p.DataType)
                {
                    case "text":
                        opt = new Option<string>(optionName)
                        {
                            Description = p.Description
                        };
                        break;
                    case "bool":
                        opt = new Option<bool>(optionName)
                        {
                            Description = p.Description
                        };
                        break;
                }
                if (opt == null)
                {
                    continue;
                }

                //opt.IsRequired = p.Priority == TemplateParameterPriority.Required;
                templateSpecificOptions.Add(p.Name, opt);
                templateGroupCommand.AddOption(opt);
            }
            return templateSpecificOptions;
        }
    }
}
