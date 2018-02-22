using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace consoledemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(JsonConvert.SerializeObject(
                new
                {
                    Greeting = "Hello World from Global Tool"
                }));
        }
    }
}