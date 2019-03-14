using System;

namespace Microsoft.Build.Execution
{
    internal interface IRunningObjectTableWrapper : IDisposable
    {
        object GetObject(string itemName);
    }
}
