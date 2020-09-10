using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal sealed class ResolveAssemblyReferenceHandler : IResolveAssemblyReferenceTaskHandler
    {
        private ResolveAssemblyReferenceTaskOutput EmptyOutput => new ResolveAssemblyReference().ResolveAssemblyReferenceOutput;

        private readonly ResolveAssemblyReference _task = new ResolveAssemblyReference();

        private ResolveAssemblyReference GetResolveAssemblyReferenceTask(IBuildEngine buildEngine)
        {
            _task.BuildEngine = buildEngine;
            _task.ResolveAssemblyReferenceOutput = EmptyOutput;

            return _task;
        }

        public Task<ResolveAssemblyReferenceResult> ExecuteAsync(ResolveAssemblyReferenceRequest input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Execute(input));

        }

        internal ResolveAssemblyReferenceResult Execute(ResolveAssemblyReferenceRequest input)
        {
            ResolveAssemblyReferenceTaskInput taskInput = new ResolveAssemblyReferenceTaskInput(input);
            ResolveAssemblyReferenceBuildEngine buildEngine = new ResolveAssemblyReferenceBuildEngine();
            ResolveAssemblyReference task = GetResolveAssemblyReferenceTask(buildEngine);
            //ResolveAssemblyReference task = new ResolveAssemblyReference
            //{
            //    BuildEngine = buildEngine
            //};

            ResolveAssemblyReferenceResult result = task.Execute(taskInput);
            result.CustomBuildEvents = buildEngine.CustomBuildEvent;
            result.BuildMessageEvents = buildEngine.MessageBuildEvent;
            result.BuildWarningEvents = buildEngine.WarningBuildEvent;
            result.BuildErrorEvents = buildEngine.ErrorBuildEvent;

            result.EventCount = buildEngine.EventCount;

            //System.Console.WriteLine("RAR task: {0}. Logged {1} events", result.TaskResult ? "Succeded" : "Failed", result.EventCount);

            return result;
        }

        public void Dispose()
        {
        }
    }
}
