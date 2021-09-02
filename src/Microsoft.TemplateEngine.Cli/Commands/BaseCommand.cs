// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseCommand : ICommandHandler
    {
        public abstract Command CreateCommand();

        public abstract Task<int> InvokeAsync(InvocationContext context);
    }

    internal abstract class BaseCommand<TArgs> : BaseCommand where TArgs : GlobalArgs
    {
        //private static readonly Guid _entryMutexGuid = new Guid("5CB26FD1-32DB-4F4C-B3DC-49CFD61633D2");
        private readonly ITemplateEngineHost _host;

        internal BaseCommand(ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks)
        {
            _host = host;
            Logger = logger;
            Callbacks = callbacks;
        }

        protected ITelemetryLogger Logger { get; }

        protected New3Callbacks Callbacks { get; }

        public override Task<int> InvokeAsync(InvocationContext context)
        {
            return ExecuteInternal(context);
        }

        protected abstract Task<int> ExecuteAsync(TArgs args, CancellationToken cancellationToken);

        //protected abstract TArgs ParseContext(InvocationContext context);

        protected IEngineEnvironmentSettings GetEnvironmentSettings(GlobalArgs globalArgs, string? outputPath)
        {
            return new EngineEnvironmentSettings(
                new CliTemplateEngineHost(_host, outputPath),
                settingsLocation: globalArgs.DebugSettingsLocation,
                virtualizeSettings: globalArgs.DebugVirtualSettings,
                environment: new CliEnvironment());
        }

        //private static Mutex GetEntryMutex(string? hivePath, ITemplateEngineHost host)
        //{
        //    string entryMutexIdentity;
        //    // this effectively mimics EngineEnvironmentSettings.BaseDir, which is not initialized when this is needed.
        //    if (!string.IsNullOrEmpty(hivePath))
        //    {
        //        entryMutexIdentity = $"{_entryMutexGuid}-{hivePath}".Replace("\\", "_").Replace("/", "_");
        //    }
        //    else
        //    {
        //        entryMutexIdentity = $"{_entryMutexGuid}-{host.HostIdentifier}-{host.Version}".Replace("\\", "_").Replace("/", "_");
        //    }

        //    return new Mutex(false, entryMutexIdentity);
        //}

        private Task<int> ExecuteInternal(InvocationContext context)
        {
            return Task.FromResult(0);
            //var args = ParseContext(context);
            //TODO: do it better - await is not supported in critical section
            //Mutex? entryMutex = null;
            //if (!args.DebugVirtualSettings)
            //{
            //    entryMutex = GetEntryMutex(args.DebugSettingsLocation, _host);
            //    entryMutex.WaitOne();
            //}
           // try
            //{
            //  //  return await ExecuteAsync(new string[] { }).ConfigureAwait(false);
            //}
            //finally
            //{
            //    //if (entryMutex != null)
            //    //{
            //    //    entryMutex.Release(1);
            //    //}
            //}
        }
    }
}
