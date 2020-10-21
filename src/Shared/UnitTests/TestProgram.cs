using System;
using System.Collections.Generic;


class Program
{
    public static int Main(string[] args)
    {
#if RUNTIME_TYPE_NETCORE
        var newArgs = new List<string>(args);
        newArgs.Insert(0, typeof(Program).Assembly.Location);

        int returnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());

        //if (showHelp)
        //{
        //    TestCommandLine.ShowHelp();
        //}

        return returnCode;
#else
        throw new NotImplementedException();
#endif
    }
}
