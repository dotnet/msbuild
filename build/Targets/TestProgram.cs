using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.NET.TestFramework;

class Program
{
    public static int Main(string[] args)
    {
        var newArgs = TestCommandLine.HandleCommandLine(args, out bool showHelp);

        //  Help argument needs to be the first one to xunit, so don't insert assembly location in that case
        if (showHelp)
        {
            newArgs.Insert(0, "/?");
        }
        else
        {
            newArgs.Insert(0, typeof(Program).Assembly.Location);
        }

        int returnCode = new Xunit.ConsoleClient.Program().EntryPoint(newArgs.ToArray());

        if (showHelp)
        {
            TestCommandLine.ShowHelp();
        }

        return returnCode;
    }
}
