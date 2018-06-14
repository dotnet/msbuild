using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

partial class Program
{
    public static int Main(string[] args)
    {
        var newArgs = args.ToList();

        //  Help argument needs to be the first one to xunit, so don't insert assembly location in that case
        if (args.Any(arg => arg.Equals("-help", StringComparison.CurrentCultureIgnoreCase) || arg.Equals("/?")))
        {
            newArgs.Insert(0, "/?");
        }
        else
        {
            newArgs.Insert(0, typeof(Program).Assembly.Location);
        }

        int returnCode = Xunit.ConsoleClient.Program.Main(newArgs.ToArray());

        return returnCode;
    }
}
