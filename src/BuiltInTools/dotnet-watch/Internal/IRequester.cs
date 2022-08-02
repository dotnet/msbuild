// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public interface IRequester
    {
        Task<ConsoleKey> GetKeyAsync(string prompt, Func<ConsoleKey, bool> validateInput, CancellationToken cancellationToken);
    }
}
