using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.NET.TestFramework;

partial class Program
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

        if (!showHelp)
        {
            BeforeTestRun(newArgs);
        }

        int returnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());

        if (showHelp)
        {
            TestCommandLine.ShowHelp();
        }
        else
        {
            AfterTestRun();
        }

        return returnCode;
    }

    static partial void BeforeTestRun(List<string> args);
    static partial void AfterTestRun();
}
