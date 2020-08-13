// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal class RarTaskHandler : IResolveAssemblyReferenceTaskHandler
    {

        public Task<int> GetNumber(int parameter)
        {
            Console.WriteLine(parameter);
            return Task.FromResult(parameter);
        }

        public void Dispose()
        {
            // For RPC dispose
        }
    }
}
