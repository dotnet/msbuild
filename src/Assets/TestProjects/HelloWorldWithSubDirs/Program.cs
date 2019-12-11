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
            // The following content is not checked in to the test assets, but generated during test execution
            // in order to circumvent certain issues like: 
            // Git Clone: Cannot clone files with long names on Windows if long file name support is not enabled
            // Nuget Pack: By default ignores files starting with "."
            string world = File.ReadAllText(Path.Combine(baseDir, "SmallNameDir", "This is a directory with a really long name for one that only contains a small file", ".word"));
            Console.WriteLine($"{hello} {world}");
        }
    }
}
