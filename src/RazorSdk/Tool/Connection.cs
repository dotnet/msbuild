// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal abstract class Connection : IDisposable
    {
        public string Identifier { get; protected set; }

        public Stream Stream { get; protected set; }

        public abstract Task WaitForDisconnectAsync(CancellationToken cancellationToken);

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
