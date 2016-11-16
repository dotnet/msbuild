using System;
using NSLib2;

namespace NSLib1
{
    public class Lib1
    {
#if NETCOREAPP1_0
        public static void HelloNCA()
        {
            Console.WriteLine("Hello World from Lib1! (netcoreapp)");
            Lib2.HelloNCA();
        }
#endif

#if NET451
        public static void HelloNet451()
        {
            Console.WriteLine("Hello World from Lib1! (net45)");
            Lib2.HelloNet451();
        }
#endif
    }
}
