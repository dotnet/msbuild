// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.Build.Execution
{
    public sealed class RarNode : INode
    {
        private readonly OutOfProcNode _msBuildNode;

        private Task _rarTask;
        private int _rarResult;

        private Task _msBuildTask;
        private Exception shutdownException;
        private NodeEngineShutdownReason shutdownReason;

        public RarNode()
        {
            _msBuildNode = new OutOfProcNode();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits",
            Justification = "We need to wait for completion of async method in synchronous method (required by interface)")]
        public NodeEngineShutdownReason Run(bool nodeReuse, bool lowPriority, out Exception shutdownException)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            var pipeName = CommunicationsUtilities.GetRarPipeName(nodeReuse, lowPriority);
            var controller = new RarController(pipeName);

            Console.CancelKeyPress += (e, sender) => cancellationTokenSource.Cancel();

            _rarTask = Task.Run(async () => _rarResult = await controller.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false));
            _msBuildTask = Task.Run(() => shutdownReason = _msBuildNode.Run(nodeReuse, lowPriority, out this.shutdownException)).WithCancellation(cancellationTokenSource.Token);

            Task.WaitAny(_msBuildTask, _rarTask);
            cancellationTokenSource.Cancel();

            shutdownException = this.shutdownException;
            return shutdownReason;
        }

        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
            return Run(false, false, out shutdownException);
        }
    }
}
