using System;
using Newtonsoft.Json;

namespace TemplateProject1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string result = JsonConvert.SerializeObject(new { a = "test" });
        }
    }
}
