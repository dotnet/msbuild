// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommand : BaseCommand<NewCommandArgs>, ICustomHelp
    {
        internal NewCommand(
            string commandName,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder,
            NewCommandCallbacks callbacks)
            : base(hostBuilder, telemetryLoggerBuilder, callbacks, commandName, SymbolStrings.Command_New_Description)
        {
            this.TreatUnmatchedTokensAsErrors = true;

            //it is important that legacy commands are built before non-legacy, as non legacy commands are building validators that rely on legacy stuff
            BuildLegacySymbols(hostBuilder, telemetryLoggerBuilder, callbacks);

            this.Add(new InstantiateCommand(hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new InstallCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new UninstallCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new UpdateCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new SearchCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new ListCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new AliasCommand(hostBuilder, telemetryLoggerBuilder, callbacks));
        }

        protected internal override IEnumerable<CompletionItem> GetCompletions(CompletionContext context, IEngineEnvironmentSettings environmentSettings)
        {
            if (context is not TextCompletionContext textCompletionContext)
            {
                foreach (CompletionItem completion in base.GetCompletions(context, environmentSettings))
                {
                    yield return completion;
                }
                yield break;
            }

            InstantiateCommand command = InstantiateCommand.FromNewCommand(this);
            CompletionContext reparsedContext = ParserFactory.CreateParser(command).Parse(textCompletionContext.CommandLineText).GetCompletionContext();
            foreach (CompletionItem completion in command.GetCompletions(reparsedContext, environmentSettings))
            {
                yield return completion;
            }

        }

        protected override Task<NewCommandStatus> ExecuteAsync(
            NewCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            ITelemetryLogger telemetryLogger,
            InvocationContext context)
        {
            InstantiateCommand command = InstantiateCommand.FromNewCommand(this);
            ParseResult reparseResult = ParserFactory.CreateParser(command).Parse(args.Tokens);
            return command.ExecuteAsync(reparseResult, environmentSettings, telemetryLogger, context);
        }

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}

