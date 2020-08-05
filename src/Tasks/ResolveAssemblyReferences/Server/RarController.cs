using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using StreamJsonRpc;
using Microsoft.Build.Tasks.ResolveAssemblyReferences;
using System.Collections.Generic;
using Nerdbank.Streams;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Server
{
    public sealed class RarController
    {
        private const int PipeBufferSize = 131072;
        
        private readonly string _pipeName;

        private readonly IResolveAssemblyReferenceTaskHandler _rarTaskHandler;

        private NamedPipeServerStream? _serverStream;


        public RarController(string pipeName)
        {
            _pipeName = pipeName;
            _rarTaskHandler = new RarTaskHandler();
        }

        public async Task<int> StartAsync(CancellationToken cancellationToken = default)
        {

            using var mutex = new ServerMutex(_pipeName, out var createdNew);

            if (!createdNew)
                return 1;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                _serverStream = GetStream(_pipeName);
                await _serverStream.WaitForConnectionAsync(cancellationToken);
                // TODO: This waits for completion of the connection, make to accept multiple connection
                await HandelConnectionAsync(_serverStream, cancellationToken);
            }
            return 0;
        }

        private async Task HandelConnectionAsync(Stream serverStream, CancellationToken cancellationToken = default)
        {
            var server = GetRpcServer(serverStream, _rarTaskHandler);
            server.StartListening();

            await server.Completion.WithCancellation(cancellationToken);
        }

        private JsonRpc GetRpcServer(Stream stream, IResolveAssemblyReferenceTaskHandler handler)
        {
            var serverHandler = RpcUtils.GetRarMessageHandler(stream);
            var rpc = new JsonRpc(serverHandler, handler);
            return rpc;
        }

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        private NamedPipeServerStream GetStream(string pipeName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(pipeName, "pipeName");

            //_sharedReadBuffer = InterningBinaryReader.CreateSharedBuffer();

#if FEATURE_PIPE_SECURITY && FEATURE_NAMED_PIPE_SECURITY_CONSTRUCTOR
            if (!NativeMethodsShared.IsMono)
            {
                var identifier = WindowsIdentity.GetCurrent().Owner;
                var security = new PipeSecurity();

                // Restrict access to just this account.  We set the owner specifically here, and on the
                // pipe client side they will check the owner against this one - they must have identical
                // SIDs or the client will reject this server.  This is used to avoid attacks where a
                // hacked server creates a less restricted pipe in an attempt to lure us into using it and
                // then sending build requests to the real pipe client (which is the MSBuild Build Manager.)
                var rule = new PipeAccessRule(identifier, PipeAccessRights.ReadWrite, AccessControlType.Allow);
                security.AddAccessRule(rule);
                security.SetOwner(identifier);

                return new NamedPipeServerStream
                    (
                    pipeName,
                    PipeDirection.InOut,
                    1, // Only allow one connection at a time.
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                    PipeBufferSize, // Default input buffer
                    PipeBufferSize,  // Default output buffer
                    security,
                    HandleInheritability.None
                );
            }
            else
#endif
            {
                return new NamedPipeServerStream
                    (
                    pipeName,
                    PipeDirection.InOut,
                    1, // Only allow one connection at a time.
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                    PipeBufferSize, // Default input buffer
                    PipeBufferSize  // Default output buffer
                );
            }
        }
    }
}
