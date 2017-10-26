using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class TestCommandLine
    {
        public static List<string> HandleCommandLine(string[] args, out bool showHelp)
        {
            //  No additional command line options for these tests beyond what xunit supports
            showHelp = false;
            return args.ToList();
        }

        public static void ShowHelp()
        {
        }
    }
}
