// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.BackEnd;

internal sealed partial class CoordinatorClient
{
    internal static class TestAccessor
    {
        /// <summary>
        ///  Attempts to connect to a coordinator using the provided settings and request a node grant.
        ///  This overload does not attempt to launch the coordinator and is intended for testing.
        /// </summary>
        /// <param name="requestedNodes">The number of nodes to request from the coordinator.</param>
        /// <param name="settings">Coordinator connection settings (pipe name, timeouts, etc.).</param>
        /// <param name="output">Debug trace output for diagnostic logging.</param>
        /// <returns>
        ///  A connected <see cref="CoordinatorClient"/> instance, or <see langword="null"/> if the connection fails.
        /// </returns>
        public static CoordinatorClient? TryConnectToServer(int requestedNodes, CoordinatorSettings settings, ICoordinatorDebugOutput output)
        {
            NamedPipeClientStream? pipeStream = null;

            try
            {
                pipeStream = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                output.WriteLine($"CoordinatorClient: Connecting to test pipe '{settings.PipeName}'");

                if (!TryConnectToPipe(pipeStream, settings.ConnectionTimeoutMs))
                {
                    output.WriteLine("CoordinatorClient: Test connection timed out");
                    return null;
                }

                output.WriteLine("CoordinatorClient: Connected to test server");

                CoordinatorClient? client = TryNegotiate(pipeStream, requestedNodes, settings, output, loggingService: null);
                pipeStream = null; // Ownership transferred unconditionally; TryNegotiate disposes on failure.
                return client;
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                output.WriteLine($"CoordinatorClient: Exception during test connect: {ex.Message}");
                return null;
            }
            finally
            {
                pipeStream?.Dispose();
            }
        }
    }
}
