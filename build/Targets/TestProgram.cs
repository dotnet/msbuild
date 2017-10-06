using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.NET.TestFramework;

class Program
{
    public static int Main(string[] args)
    {
        var newArgs = TestCommandLine.HandleCommandLine(args);

        newArgs.Insert(0, typeof(Program).Assembly.Location);

        int returnCode = new Xunit.ConsoleClient.Program().EntryPoint(newArgs.ToArray());

        return returnCode;
    }
}
