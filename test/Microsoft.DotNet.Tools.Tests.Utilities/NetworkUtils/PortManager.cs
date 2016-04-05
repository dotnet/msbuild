using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class PortManager
    {
        private static int s_nextPort = 8001;

        public static int GetPort()
        {
            return Interlocked.Increment(ref s_nextPort);
        }
    }
}
