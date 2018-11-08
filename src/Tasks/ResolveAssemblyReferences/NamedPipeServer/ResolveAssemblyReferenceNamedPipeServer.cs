using System.IO.Pipes;
using System.Threading;

using Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Engine;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Serialization;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.NamedPipeServer
{
    public class ResolveAssemblyReferenceNamedPipeServer
    {
        private const string PipeName = "ResolveAssemblyReference.Pipe";

        private ResolveAssemblyReferenceCacheService RarService { get; }

        private int NumThreads { get; } = 1;

        public ResolveAssemblyReferenceNamedPipeServer(int numThreads = 1)
        {
            System.Threading.Tasks.Task.Run(() => { BondDeserializer<ResolveAssemblyReferenceRequest>.Initialize(); });
            System.Threading.Tasks.Task.Run(() => { BondSerializer<ResolveAssemblyReferenceResponse>.Initialize(); });

            var rarTask = new ResolveAssemblyReferenceStatelessTask();
            var taskGatewayService = new ResolveAssemblyReferenceTaskGateway(rarTask);

            var directoryWatcher = new DirectoryWatcher();
            var cacheService = new ResolveAssemblyReferenceCacheService(taskGatewayService, directoryWatcher);

            RarService = cacheService;
            NumThreads = numThreads;
        }

        public void Start()
        {
            for (int i = 0; i < NumThreads; i++)
            {
                new Thread(RunPipeServer).Start();
            }
        }

        private void RunPipeServer()
        {
            var pipe = new NamedPipeServerStream
            (
                PipeName,
                PipeDirection.InOut,
                NumThreads,
                PipeTransmissionMode.Byte,
                PipeOptions.WriteThrough,
                16384,
                16384
            );

            while (true)
            {
                pipe.WaitForConnection();
                HandleRequest(pipe);
                pipe.WaitForPipeDrain();
                pipe.Disconnect();
            }
        }

        private void HandleRequest(NamedPipeServerStream pipe)
        {
            ResolveAssemblyReferenceRequest req = BondDeserializer<ResolveAssemblyReferenceRequest>.Deserialize(pipe);
            var resp = RarService.ResolveAssemblyReferences(req);
            BondSerializer<ResolveAssemblyReferenceResponse>.Serialize(pipe, resp);
        }
    }
}
