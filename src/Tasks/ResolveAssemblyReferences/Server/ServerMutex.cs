using System;
using System.Threading;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Server
{
    internal sealed class ServerMutex : IDisposable
    {
        private readonly Mutex _mutex;
        public bool IsLocked { get; private set; }
        public bool IsDisposed { get; private set; }

        public ServerMutex(string name, out bool createdNew)
        {
            _mutex = new Mutex(true, name, out createdNew);
            if (createdNew)
                IsLocked = true;
        }

        public void Dispose()
        {

            if (IsDisposed)
                return;
            IsDisposed = true;

            try
            {
                if (IsLocked)
                    _mutex.ReleaseMutex();
            }
            finally
            {
                _mutex.Dispose();
                IsLocked = false;
            }
        }
    }
}
