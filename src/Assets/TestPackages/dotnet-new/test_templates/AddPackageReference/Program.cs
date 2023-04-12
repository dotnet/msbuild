using System;
using Newtonsoft.Json;

namespace Basic
{
    class MyClass
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string result = JsonConvert.SerializeObject(new { a = "test" });
        }
    }
}
