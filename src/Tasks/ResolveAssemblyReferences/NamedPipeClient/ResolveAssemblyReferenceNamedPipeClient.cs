using System.IO.Pipes;

using Microsoft.Build.Tasks.ResolveAssemblyReferences.Abstractions;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Engine;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Serialization;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.NamedPipeClient
{
    internal class ResolveAssemblyReferenceNamedPipeClient : IResolveAssemblyReferenceService
    {
        private const string PipeName = "ResolveAssemblyReference.Pipe";

        private ResolveAssemblyReferenceServiceGateway RarTask { get; }

        internal ResolveAssemblyReferenceNamedPipeClient()
        {
            // TODO: Make this a little clearer. Think it's a little confusing for the class to pass itself into the gateway and going
            // namedPipeClient -> serviceGateway -> namedPipeClient
            // but it also seems boilerplate-y to separate the IResolveAssemblyReferenceService implementation
            // into services and have both ResolveAssemblyReferenceNamedPipeClient and
            // ResolveAssemblyReferenceNamedPipeClientService
            RarTask = new ResolveAssemblyReferenceServiceGateway(this);
        }

        internal ResolveAssemblyReferenceTaskOutput Execute(ResolveAssemblyReferenceTaskInput taskInput)
        {
            return RarTask.Execute(taskInput);
        }

        public ResolveAssemblyReferenceResponse ResolveAssemblyReferences(ResolveAssemblyReferenceRequest req)
        {
            using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.WriteThrough))
            {
                pipe.Connect();
                BondSerializer<ResolveAssemblyReferenceRequest>.Serialize(pipe, req);
                return BondDeserializer<ResolveAssemblyReferenceResponse>.Deserialize(pipe);
            }
        }
    }
}
