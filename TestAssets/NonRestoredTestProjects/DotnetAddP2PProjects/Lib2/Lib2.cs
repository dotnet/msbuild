using System;
using NSLib4;

namespace NSLib2
{
    public class Lib2
    {
#if NETCOREAPP1_0
        public static void HelloNCA()
        {
            Console.WriteLine("Hello World from Lib2! (netcoreapp)");
            Lib4.HelloNCA();
        }
#endif

#if NET451
        public static void HelloNet451()
        {
            Console.WriteLine("Hello World from Lib2! (net45)");
            Lib4.HelloNet451();
        }
#endif
    }
}
