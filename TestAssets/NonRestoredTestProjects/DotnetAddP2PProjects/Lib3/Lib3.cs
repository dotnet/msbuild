using System;

namespace NSLib3
{
    public class Lib3
    {
#if NETCOREAPP1_0
        public static void HelloNCA()
        {
            Console.WriteLine("Hello World from Lib3! (netcoreapp)");
        }
#endif

#if NET451
        public static void HelloNet451()
        {
            Console.WriteLine("Hello World from Lib3! (net45)");
        }
#endif
    }
}
