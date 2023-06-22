#if (csharpFeature_TopLevelProgram)
// See https://aka.ms/new-console-template for more information
#endif
#if (!csharpFeature_ImplicitUsings)
using System;

#endif
#if (csharpFeature_TopLevelProgram)
Console.WriteLine("Hello, World!");
#else
#if (csharpFeature_FileScopedNamespaces)
namespace Company.ConsoleApplication1;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}
#else
namespace Company.ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
        }
    }
}
#endif
#endif
