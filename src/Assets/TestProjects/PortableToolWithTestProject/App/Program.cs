using Newtonsoft.Json;
using System;

namespace consoledemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(JsonConvert.SerializeObject(
                new
                {
                    Greeting = "Hello World from Global Tool"
                }));
        }
    }
}
