using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CSharp.RuntimeBinder;

namespace AutoAddDesktopReferencesDuringMigrate
{
    class Program
    {
        static void Main(string[] args)
        {
            var mscorlibRef = new List<int>(new int[] { 4, 5, 6 });
            var systemCoreRef = mscorlibRef.ToArray().Average();
            Debug.Assert(systemCoreRef == 5, "Test System assembly reference");
            if (systemCoreRef != 5)
            {
                throw new RuntimeBinderException("Test Microsoft.CSharp assembly reference");
            }
        }
    }
}
