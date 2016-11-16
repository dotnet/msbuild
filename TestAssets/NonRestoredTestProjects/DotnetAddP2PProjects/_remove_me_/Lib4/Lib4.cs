using System;

namespace NSLib4
{
    public class Lib4
    {
#if NETCOREAPP1_0
        public static void HelloNCA()
        {
            Console.WriteLine("Hello World from Lib4! (netcoreapp)");
        }
#endif

#if NET451
        public static void HelloNet451()
        {
            Console.WriteLine("Hello World from Lib4! (net45)");
        }
#endif
    }
}
