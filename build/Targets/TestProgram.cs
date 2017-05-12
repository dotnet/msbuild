using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    public static void Main(string[] args)
    {
        var newArgs = args.ToList();
        newArgs.Insert(0, typeof(Program).Assembly.Location);
        new Xunit.ConsoleClient.Program().EntryPoint(newArgs.ToArray());
    }
}
