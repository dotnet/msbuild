using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// API for controller of ResolveAssemblyReference node
    /// </summary>
    internal interface IRarController
    {
        Task<int> StartAsync(CancellationToken token);
    }
}
