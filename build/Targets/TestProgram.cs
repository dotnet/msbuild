using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    public static int Main(string[] args)
    {
        var newArgs = args.ToList();
        newArgs.Insert(0, typeof(Program).Assembly.Location);

        int returnCode = new Xunit.ConsoleClient.Program().EntryPoint(newArgs.ToArray());

        return returnCode;
    }
}
