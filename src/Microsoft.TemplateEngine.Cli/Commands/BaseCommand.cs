// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface IBaseCommand
    {
        public Command CreateCommand();

        public Task<int> InvokeAsync(InvocationContext context);
    }

    internal abstract class BaseCommand<TArgs> : IBaseCommand, ICommandHandler where TArgs : GlobalArgs
    {
        private static readonly Guid _entryMutexGuid = new Guid("5CB26FD1-32DB-4F4C-B3DC-49CFD61633D2");
        private readonly ITemplateEngineHost _host;

        internal BaseCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks)
        {
            _host = host;
            TelemetryLogger = logger;
            Callbacks = callbacks;
        }

        protected ITelemetryLogger TelemetryLogger { get; }

        protected New3Callbacks Callbacks { get; }

        public Command CreateCommand()
        {
            var command = CreateCommandAbstract();
            GlobalArgs.AddGlobalsToCommand(command);
            command.Handler = this;
            return command;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TArgs args = ParseContext(context);

            string? outputPath = (args as InstantiateCommandArgs)?.OutputPath;

            IEngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(
                new CliTemplateEngineHost(_host, outputPath),
                settingsLocation: args.DebugSettingsLocation,
                virtualizeSettings: args.DebugVirtualSettings,
                environment: new CliEnvironment());

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            using AsyncMutex? entryMutex = await EnsureEntryMutex(args, environmentSettings, cancellationTokenSource.Token).ConfigureAwait(false);
            return (int)await ExecuteAsync(args, environmentSettings, cancellationTokenSource.Token).ConfigureAwait(false);
        }

        protected abstract Command CreateCommandAbstract();

        protected abstract Task<New3CommandStatus> ExecuteAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken);

        protected abstract TArgs ParseContext(InvocationContext context);

        private static async Task<AsyncMutex?> EnsureEntryMutex(TArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken token)
        {
            // we don't need to acquire mutex in case of virtual settings
            if (args.DebugVirtualSettings)
            {
                return null;
            }
            string entryMutexIdentity = $"Global\\{_entryMutexGuid}_{environmentSettings.Paths.HostVersionSettingsDir.Replace("\\", "_").Replace("/", "_")}";
            return await AsyncMutex.WaitAsync(entryMutexIdentity, token).ConfigureAwait(false);
        }
    }
}
