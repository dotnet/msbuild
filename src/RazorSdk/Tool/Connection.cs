// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
