using NSLib1;
using NSLib3;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
#if NETCOREAPP1_0
            Lib1.HelloNCA();
            Lib3.HelloNCA();
#endif

#if NET451
            Lib1.HelloNet451();
            Lib3.HelloNet451();
#endif
        }
    }
}
