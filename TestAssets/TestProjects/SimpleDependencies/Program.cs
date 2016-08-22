using System;
using System.Collections;
using Newtonsoft.Json.Linq;

class Program
{
    public static void Main(string[] args)
    {
        // use ArrayList to make sure System.Collections.NonGeneric is used
        ArrayList argList = new ArrayList(args);
        JObject jObject = new JObject();

        foreach (string arg in argList)
        {
            jObject[arg] = arg;
        }

        Console.WriteLine(jObject.ToString());
    }
}
