// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class TemplateGroupCommand : Command, ICommandHandler
    {
        private IEngineEnvironmentSettings _environmentSettings;
        private ITemplateInfo _template;
        private NewCommand _newCommand;
        private Dictionary<string, Option> _templateSpecificOptions;

        public TemplateGroupCommand(NewCommand newCommand, IEngineEnvironmentSettings environmentSettings, ITemplateInfo template) : base(template.ShortNameList[0], template.Name + Environment.NewLine + template.Description)
        {
            _newCommand = newCommand;
            _environmentSettings = environmentSettings;
            foreach (var item in template.ShortNameList.Skip(1))
            {
                AddAlias(item);
            }
            _templateSpecificOptions = TemplateGroupArgs.AddToCommand(this, template);
            this.Handler = this;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TemplateGroupArgs templateGroupArgs = new TemplateGroupArgs(_template, context.ParseResult, _templateSpecificOptions);

            if (templateGroupArgs.HelpRequested)
            {
                HelpResult helpResult = new HelpResult();
                helpResult.Apply(context);
                return 0;
            }

            var instatiatior = new TemplateInvoker(_environmentSettings, _newCommand.TelemetryLogger, () => Console.ReadLine() ?? string.Empty, _newCommand.Callbacks, new HostSpecificDataLoader(_environmentSettings));
            var result = await instatiatior.InvokeTemplate(templateGroupArgs).ConfigureAwait(false);
            return (int)result;
        }
    }
}
