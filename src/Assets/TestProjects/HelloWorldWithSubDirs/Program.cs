using System;
using System.IO;
using System.Reflection;

namespace HelloWorldWithSubDirs
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string hello = File.ReadAllText(Path.Combine(baseDir, "SmallNameDir", "word"));
            string world = File.ReadAllText(Path.Combine(baseDir, "SmallNameDir", "This is a directory with a really long name for one that only contains a small file", ".word"));
            Console.WriteLine($"{hello} {world}");
        }
    }
}
