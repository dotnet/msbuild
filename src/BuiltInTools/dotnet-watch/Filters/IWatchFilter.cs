// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tools
{
    internal interface IWatchFilter
    {
        ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken);
    }
}
