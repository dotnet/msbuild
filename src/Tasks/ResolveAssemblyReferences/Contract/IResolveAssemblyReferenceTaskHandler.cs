using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    public interface IResolveAssemblyReferenceTaskHandler : IDisposable
    {
        Task<int> GetNumber(int parameter);
    }
}
