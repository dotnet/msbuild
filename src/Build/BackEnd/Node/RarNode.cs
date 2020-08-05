using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Execution
{
    public sealed class RarNode : INode
    {
        private readonly OutOfProcNode _msBuildNode;

        private Task _rarTask;
        private int rarResult;

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

            var pipeName = CommunicationsUtilities.GetRARPipeName(nodeReuse, lowPriority);
            var controller = new RarController(pipeName);

            Console.CancelKeyPress += (e, sender) => cancellationTokenSource.Cancel();

            _rarTask = Task.Run(async () => rarResult = await controller.StartAsync(cancellationTokenSource.Token));
            _msBuildTask = Task.Run(() => shutdownReason = _msBuildNode.Run(nodeReuse, lowPriority, out this.shutdownException)).WithCancellation(cancellationTokenSource.Token);

            Task.WaitAny(_msBuildTask, _rarTask);
            cancellationTokenSource.Cancel();
            Console.ReadLine();
            shutdownException = this.shutdownException;
            return shutdownReason;
        }

        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
            return Run(false, false, out shutdownException);
        }
    }
}
