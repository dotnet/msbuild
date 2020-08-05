using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal class RarTaskHandler : IResolveAssemblyReferenceTaskHandler
    {

        public Task<int> GetNumber(int parameter)
        {
            Console.WriteLine(parameter);
            Console.WriteLine();
            return Task.FromResult(parameter);
        }

        public void Dispose()
        {
            // For RPC dispose
        }
    }
}
