// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal abstract class RequestDispatcher
    {
        /// <summary>
        /// Default time the server will stay alive after the last request disconnects.
        /// </summary>
        public static readonly TimeSpan DefaultServerKeepAlive = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Time to delay after the last connection before initiating a garbage collection
        /// in the server.
        /// </summary>
        public static readonly TimeSpan GCTimeout = TimeSpan.FromSeconds(30);

        public abstract void Run();

        public static RequestDispatcher Create(ConnectionHost connectionHost, CompilerHost compilerHost, CancellationToken cancellationToken, EventBus eventBus, TimeSpan? keepAlive = null)
        {
            return new DefaultRequestDispatcher(connectionHost, compilerHost, cancellationToken, eventBus, keepAlive);
        }
    }
}
