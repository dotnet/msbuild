using System;
using System.Xml;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("Hello World!");
#if NET20 || NET35 || NET45 || NET461
            // Force XmlDocument to be used
            var doc = new XmlDocument();
#endif
        }
    }
}
